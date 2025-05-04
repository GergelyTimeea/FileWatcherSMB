using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Threading;

class Program
{
    // Coada de evenimente
    private static ConcurrentQueue<(string EventType, string Path, string? OldPath)> eventCache = new();
    private static bool isRunning = true;

    // Cache pentru deduplicare evenimente
    private static MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());

    static void Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string pathToWatch = config["NfsWatcher:WatchPath"];

        if (string.IsNullOrWhiteSpace(pathToWatch) || !Directory.Exists(pathToWatch))
        {
            Console.WriteLine($"Eroare: Calea specificată nu există sau nu e validă: {pathToWatch}");
            return;
        }

        try
        {
            using var watcher = new FileSystemWatcher(pathToWatch);
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            Thread eventProcessor = new Thread(ProcessEvents);
            eventProcessor.Start();

            Console.WriteLine($"Monitorizare pornită pentru: {pathToWatch}");
            Console.WriteLine("Apasă Enter pentru a opri...");
            Console.ReadLine();

            isRunning = false;
            eventProcessor.Join();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Eroare la pornirea watcher-ului: {ex.Message}");
        }
    }

    private static bool IsTemporaryFile(string path)
    {
        string name = Path.GetFileName(path).ToLower();

        return name.StartsWith("~$") || name.StartsWith(".~lock.") ||
               name.EndsWith(".tmp") || name.EndsWith(".temp") ||
               name.EndsWith(".swp") || name.EndsWith(".swx") ||
               name == ".ds_store" || name == "thumbs.db";
    }

    private static bool IsDuplicateEvent(string eventType, string path, string? oldPath = null)
    {
        string key = eventType + "|" + path + "|" + oldPath;
        if (memoryCache.TryGetValue(key, out _))
            return true;

        memoryCache.Set(key, true, TimeSpan.FromSeconds(2));
        return false;
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicateEvent("CREAT", e.FullPath)) return;
        eventCache.Enqueue(("CREAT", e.FullPath, null));
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicateEvent("MODIFICAT", e.FullPath)) return;
        if (e.ChangeType == WatcherChangeTypes.Changed)
            eventCache.Enqueue(("MODIFICAT", e.FullPath, null));
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicateEvent("STERS", e.FullPath)) return;
        eventCache.Enqueue(("STERS", e.FullPath, null));
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;
        if (IsDuplicateEvent("REDENUMIT", e.FullPath, e.OldFullPath)) return;
        eventCache.Enqueue(("REDENUMIT", e.FullPath, e.OldFullPath));
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"[EROARE] Watcher a întâmpinat o problemă: {e.GetException().Message}");
    }

    private static void ProcessEvents()
    {
        while (isRunning || !eventCache.IsEmpty)
        {
            if (eventCache.TryDequeue(out var ev))
            {
                if (ev.EventType == "REDENUMIT")
                    Console.WriteLine($"[REDENUMIT] {ev.OldPath} -> {ev.Path}");
                else
                    Console.WriteLine($"[{ev.EventType}] {ev.Path}");
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }
}
