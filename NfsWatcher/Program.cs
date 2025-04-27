using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Threading;

class Program
{
    // Cache pentru evenimente (thread-safe)
    private static ConcurrentQueue<(string EventType, string Path, string? OldPath)> eventCache = new();
    private static bool isRunning = true;

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
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime;

            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            // Thread separat pentru a procesa evenimentele din cache
            Thread eventProcessor = new Thread(ProcessEvents);
            eventProcessor.Start();

            Console.WriteLine($"Monitorizare pornită pentru: {pathToWatch}");
            Console.WriteLine("Apasă Enter pentru a opri...");
            Console.ReadLine();
            isRunning = false; // oprește threadul de procesare
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

        // Prefix
        if (name.StartsWith("~$")) return true;
        if (name.StartsWith(".~lock.")) return true;

        // Sufix/extensie
        if (name.EndsWith(".tmp")) return true;
        if (name.EndsWith(".temp")) return true;
        if (name.EndsWith(".swp")) return true;
        if (name.EndsWith(".swx")) return true;
        if (name == ".ds_store") return true;
        if (name == "thumbs.db") return true;

        return false;
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath)) return;
        eventCache.Enqueue(("CREAT", e.FullPath, null));
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath)) return;
        if (e.ChangeType == WatcherChangeTypes.Changed)
            eventCache.Enqueue(("MODIFICAT", e.FullPath, null));
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath)) return;
        eventCache.Enqueue(("STERS", e.FullPath, null));
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;
        eventCache.Enqueue(("REDENUMIT", e.FullPath, e.OldFullPath));
    }


    private static void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"[EROARE] Watcher a întâmpinat o problemă: {e.GetException().Message}");
    }

    // Thread separat: procesează evenimentele (doar cele non-temporare)
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
