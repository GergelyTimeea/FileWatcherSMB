using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

class Program
{
    private static ConcurrentQueue<(string EventType, string Path, string? OldPath)> eventCache = new();
    private static bool isRunning = true;
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

    private static bool IsDuplicate(string eventType, string path, string? oldPath = null)
    {
        string key = eventType + "|" + path + "|" + oldPath;

        if (memoryCache.TryGetValue(key, out _))
            return true;

        memoryCache.Set(key, true, TimeSpan.FromMilliseconds(800)); // debounce + deduplicare
        return false;
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicate("CREAT", e.FullPath)) return;
        eventCache.Enqueue(("CREAT", e.FullPath, null));
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicate("MODIFICAT", e.FullPath)) return;
        if (e.ChangeType == WatcherChangeTypes.Changed)
            eventCache.Enqueue(("MODIFICAT", e.FullPath, null));
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicate("STERS", e.FullPath)) return;
        eventCache.Enqueue(("STERS", e.FullPath, null));
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {

        if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;
        if (IsDuplicate("REDENUMIT", e.FullPath, e.OldFullPath)) return;
        eventCache.Enqueue(("REDENUMIT", e.FullPath, e.OldFullPath));
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"[EROARE] Watcher a întâmpinat o problemă: {e.GetException().Message}");
    }

    private static void ProcessEvents()
    {
        var pendingEvents = new List<(string EventType, string Path, string? OldPath, DateTime Time)>();

        while (isRunning || !eventCache.IsEmpty || pendingEvents.Any())
        {
            while (eventCache.TryDequeue(out var ev))
            {
                pendingEvents.Add((ev.EventType, ev.Path, ev.OldPath, DateTime.Now));
            }

            var now = DateTime.Now;
            var toProcess = pendingEvents
                .Where(e => (now - e.Time).TotalMilliseconds > 800)
                .ToList();

            //Se selectează din listă evenimentele care au stat acolo cel puțin 800 ms.
            //este necesară pentru a permite apariția altor evenimente care au legătură(de exemplu CREAT → MODIFICAT imediat după).
            foreach (var e in toProcess)
            {
                var related = pendingEvents
                    .Where(x =>
                        (x.Path == e.Path || (x.OldPath != null && x.OldPath == e.OldPath)) &&
                        (now - x.Time).TotalMilliseconds <= 2000)
                    .ToList();

                var relatedEvents = related.Select(x => x.EventType).ToList();

                bool isRedenModificat = relatedEvents.Contains("REDENUMIT") && relatedEvents.Contains("MODIFICAT");
                //redenumit și modificat → MODIFICAT(problema la word)
                bool isSterCreat = relatedEvents.Contains("STERS") && relatedEvents.Contains("CREAT") && !relatedEvents.Contains("MODIFICAT");
                //STERS și CREAT → REDENUMIT.(linux)


                bool isSterCreatModificat = relatedEvents.Contains("STERS") && relatedEvents.Contains("CREAT") && relatedEvents.Contains("MODIFICAT");
                bool isCreatModificat = relatedEvents.Contains("CREAT") && relatedEvents.Contains("MODIFICAT") &&
                                        !relatedEvents.Contains("STERS") && !relatedEvents.Contains("REDENUMIT");

                bool isRedenIsActuallyModif = related.Count == 1 &&
                    related[0].EventType == "REDENUMIT" &&
                    related[0].OldPath != null &&
                    Path.GetFileName(related[0].OldPath) == Path.GetFileName(related[0].Path) &&
                    Path.GetExtension(related[0].OldPath) == Path.GetExtension(related[0].Path);
                //redenumit cu nume identic -> Modificat

                if (isSterCreatModificat)
                {
                    //STERS + CREAT + MODIFICAT
                    // Dacă fișierul a fost redenumit real (nu doar înlocuit), atunci tratăm ca redenumire
                    if (e.OldPath != null && Path.GetFileName(e.OldPath) != Path.GetFileName(e.Path))
                    {
                        Console.WriteLine($"[REDENUMIT] {e.OldPath} -> {e.Path}");
                    }
                    else
                    {
                        Console.WriteLine($"[MODIFICAT] {e.Path}");
                    }

                    pendingEvents.RemoveAll(x => x.Path == e.Path || x.OldPath == e.OldPath);
                    break;
                }

                //tratăm ca o modificare dacă redenumirea a fost doar un mecanism intern.
                if (isRedenModificat || isRedenIsActuallyModif)
                {
                    Console.WriteLine($"[MODIFICAT] {e.Path}");
                    pendingEvents.RemoveAll(x => x.Path == e.Path || x.OldPath == e.OldPath);
                    break;
                }

                //creat + modificat -> creat
                if (isCreatModificat)
                {
                    Console.WriteLine($"[CREAT] {e.Path}");
                    pendingEvents.RemoveAll(x => x.Path == e.Path);
                    break;
                }


                if (isSterCreat)
                {
                    Console.WriteLine($"[REDENUMIT] {e.OldPath} -> {e.Path}");
                    pendingEvents.RemoveAll(x => x.Path == e.Path || x.OldPath == e.OldPath);
                    break;
                }

                if (related.Count == 1)
                {
                    var evType = related[0].EventType;
                    if (evType == "CREAT")
                        Console.WriteLine($"[CREAT] {e.Path}");
                    else if (evType == "MODIFICAT")
                        Console.WriteLine($"[MODIFICAT] {e.Path}");
                    else if (evType == "REDENUMIT")
                        Console.WriteLine($"[REDENUMIT] {e.OldPath} -> {e.Path}");
                    else if (evType == "STERS")
                        Console.WriteLine($"[STERS] {e.Path}");

                    pendingEvents.Remove(e);
                    break;
                }

                Console.WriteLine($"[{e.EventType}] {e.Path}");
                pendingEvents.Remove(e);
            }

            Thread.Sleep(200);
        }
    }
}