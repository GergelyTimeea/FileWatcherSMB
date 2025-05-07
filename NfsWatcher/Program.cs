using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;

class FileMonitor
{
    private static readonly ConcurrentQueue<(string EventType, string Path, string? OldPath)> eventQueue = new();
    private static readonly MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
    private static bool isRunning = true;

    public static void Main(string[] args)
    {
        string pathToWatch = @"\\172.20.10.6\shared"; // Replace with your shared folder path

        if (string.IsNullOrWhiteSpace(pathToWatch) || !Directory.Exists(pathToWatch))
        {
            Console.WriteLine($"Error: The specified path does not exist or is invalid: {pathToWatch}");
            return;
        }

        try
        {
            using var watcher = new FileSystemWatcher(pathToWatch)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size |
                               NotifyFilters.CreationTime
            };

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            Thread eventProcessor = new Thread(ProcessEvents);
            eventProcessor.Start();

            Console.WriteLine($"Monitoring started for: {pathToWatch}");
            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();

            isRunning = false;
            eventProcessor.Join();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting the watcher: {ex.Message}");
        }
    }

    private static bool IsTemporaryFile(string path)
    {
        string name = Path.GetFileName(path).ToLower();
        return name.StartsWith("~$") || name.StartsWith(".~lock.") ||
               name.EndsWith(".tmp") || name.EndsWith(".temp") ||
               name.EndsWith(".swp") || name.EndsWith(".swx") ||
               name.EndsWith(".goutputstream") || name.Contains(".goutputstream") ||
               name.EndsWith(".part") || name == ".ds_store" || name == "thumbs.db";
    }

    private static bool IsDuplicateEvent(string eventType, string path, string? oldPath = null)
    {
        string key = $"{eventType}|{path}|{oldPath}";
        if (memoryCache.TryGetValue(key, out _))
            return true;

        memoryCache.Set(key, true, TimeSpan.FromSeconds(2));
        return false;
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath)) return;

        if (!IsDuplicateEvent("CREATED", e.FullPath))
            eventQueue.Enqueue(("CREATED", e.FullPath, null));
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath)) return;

        if (!IsDuplicateEvent("CHANGED", e.FullPath))
            eventQueue.Enqueue(("CHANGED", e.FullPath, null));
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath)) return;

        if (!IsDuplicateEvent("DELETED", e.FullPath))
            eventQueue.Enqueue(("DELETED", e.FullPath, null));
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;

        if (!IsDuplicateEvent("RENAMED", e.FullPath, e.OldFullPath))
            eventQueue.Enqueue(("RENAMED", e.FullPath, e.OldFullPath));
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"[ERROR] Watcher encountered an issue: {e.GetException().Message}");
    }

    private static void ProcessEvents()
    {
        while (isRunning || !eventQueue.IsEmpty)
        {
            var eventsBatch = new List<(string EventType, string Path, string? OldPath)>();
            while (eventQueue.TryDequeue(out var ev))
            {
                eventsBatch.Add(ev);

                // Limit batch size to 50 events to avoid indefinite processing
                if (eventsBatch.Count >= 50) break;
            }

            if (eventsBatch.Count > 0)
            {
                ProcessBatch(eventsBatch);
            }
            else
            {
                Thread.Sleep(100); // No events to process, sleep briefly
            }
        }
    }

    private static void ProcessBatch(List<(string EventType, string Path, string? OldPath)> events)
    {
        var groupedByPath = events.GroupBy(e => e.Path).ToList();

        foreach (var group in groupedByPath)
        {
            var path = group.Key;
            var eventsForPath = group.ToList();

            // Handle patterns for Linux file save behavior
            if (eventsForPath.Any(e => e.EventType == "RENAMED"))
            {
                var renameEvent = eventsForPath.FirstOrDefault(e => e.EventType == "RENAMED");
                if (renameEvent != default)
                {
                    // Check if this is an intermediate rename (*filename -> filename)
                    if (renameEvent.OldPath != null && renameEvent.OldPath.StartsWith("*") &&
                        renameEvent.Path == renameEvent.OldPath.TrimStart('*'))
                    {
                        // Check if the file size or content changed
                        var fileInfo = new FileInfo(renameEvent.Path);
                        if (fileInfo.Exists)
                        {
                            Console.WriteLine($"[MODIFIED] {renameEvent.Path}");
                        }
                        else
                        {
                            Console.WriteLine($"[RENAMED] {renameEvent.OldPath} -> {renameEvent.Path}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[RENAMED] {renameEvent.OldPath} -> {renameEvent.Path}");
                    }
                }
            }
            else if (eventsForPath.Any(e => e.EventType == "CHANGED"))
            {
                Console.WriteLine($"[MODIFIED] {path}");
            }
            else if (eventsForPath.Any(e => e.EventType == "CREATED"))
            {
                Console.WriteLine($"[CREATED] {path}");
            }
            else if (eventsForPath.Any(e => e.EventType == "DELETED"))
            {
                Console.WriteLine($"[DELETED] {path}");
            }
        }
    }
}