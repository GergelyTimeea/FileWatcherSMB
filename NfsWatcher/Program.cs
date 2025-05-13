using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    private static readonly List<(string EventType, string Path, string? OldPath, DateTime Time)> eventBuffer = new();
    private static readonly object bufferLock = new();

    private static bool isRunning = true;
    private static HashSet<string> eventDeduplicationSet = new();
    private static readonly object deduplicationLock = new();

    private static Dictionary<string, DateTime> antivirusQueue = new();

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
               name.StartsWith(".goutputstream-") ||
               name == ".ds_store" || name == "thumbs.db";
    }

    private static bool IsDuplicate(string eventType, string path, string? oldPath = null)
    {
        string key = eventType + "|" + path + "|" + oldPath;
        lock (deduplicationLock)
        {
            if (eventDeduplicationSet.Contains(key)) return true;
            eventDeduplicationSet.Add(key);
            _ = Task.Delay(800).ContinueWith(_ =>
            {
                lock (deduplicationLock)
                {
                    eventDeduplicationSet.Remove(key);
                }
            });
            return false;
        }
    }

    private static void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicate("CREAT", e.FullPath)) return;
        lock (bufferLock)
        {
            eventBuffer.Add(("CREAT", e.FullPath, null, DateTime.Now));
        }
    }

    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicate("MODIFICAT", e.FullPath)) return;
        if (e.ChangeType == WatcherChangeTypes.Changed)
        {
            lock (bufferLock)
            {
                eventBuffer.Add(("MODIFICAT", e.FullPath, null, DateTime.Now));
            }
        }
    }

    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsDuplicate("STERS", e.FullPath)) return;
        lock (bufferLock)
        {
            eventBuffer.Add(("STERS", e.FullPath, null, DateTime.Now));
        }
    }

    private static void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;
        if (IsDuplicate("REDENUMIT", e.FullPath, e.OldFullPath)) return;
        lock (bufferLock)
        {
            eventBuffer.Add(("REDENUMIT", e.FullPath, e.OldFullPath, DateTime.Now));
        }
    }

    private static void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"[EROARE] Watcher a întâmpinat o problemă: {e.GetException().Message}");
    }

    private static void ProcessEvents()
    {
        while (isRunning || eventBuffer.Count > 0)
        {
            List<(string EventType, string Path, string? OldPath, DateTime Time)> toProcess;

            lock (bufferLock)
            {
                toProcess = eventBuffer
                    .Where(e => (DateTime.Now - e.Time).TotalMilliseconds > 800)
                    .ToList();
            }

            foreach (var e in toProcess)
            {
                List<(string EventType, string Path, string? OldPath, DateTime Time)> related;
                lock (bufferLock)
                {
                    related = eventBuffer
                        .Where(x =>
                            (x.Path == e.Path || (x.OldPath != null && x.OldPath == e.OldPath)) &&
                            (DateTime.Now - x.Time).TotalMilliseconds <= 2000)
                        .ToList();
                }

                var relatedEvents = related.Select(x => x.EventType).ToList();

                bool isRedenModificat = relatedEvents.Contains("REDENUMIT") && relatedEvents.Contains("MODIFICAT");
                bool isSterCreat = relatedEvents.Contains("STERS") && relatedEvents.Contains("CREAT") && !relatedEvents.Contains("MODIFICAT");
                bool isSterCreatModificat = relatedEvents.Contains("STERS") && relatedEvents.Contains("CREAT") && relatedEvents.Contains("MODIFICAT");
                bool isCreatModificat = relatedEvents.Contains("CREAT") && relatedEvents.Contains("MODIFICAT") &&
                                        !relatedEvents.Contains("STERS") && !relatedEvents.Contains("REDENUMIT");

                bool isRedenIsActuallyModif = related.Count == 1 &&
                    related[0].EventType == "REDENUMIT" &&
                    related[0].OldPath != null &&
                    Path.GetFileName(related[0].OldPath) == Path.GetFileName(related[0].Path) &&
                    Path.GetExtension(related[0].OldPath) == Path.GetExtension(related[0].Path);

                if (isSterCreatModificat)
                {
                    if (e.OldPath != null && Path.GetFileName(e.OldPath) != Path.GetFileName(e.Path))
                    {
                        Console.WriteLine($"[REDENUMIT] {e.OldPath} -> {e.Path}");
                    }
                    else
                    {
                        Console.WriteLine($"[MODIFICAT] {e.Path}");
                        antivirusQueue[e.Path] = DateTime.Now;
                    }

                    lock (bufferLock)
                    {
                        eventBuffer.RemoveAll(x => x.Path == e.Path || x.OldPath == e.OldPath);
                    }
                    break;
                }

                if (isRedenModificat || isRedenIsActuallyModif)
                {
                    Console.WriteLine($"[MODIFICAT] {e.Path}");
                    antivirusQueue[e.Path] = DateTime.Now;
                    lock (bufferLock)
                    {
                        eventBuffer.RemoveAll(x => x.Path == e.Path || x.OldPath == e.OldPath);
                    }
                    break;
                }

                if (isCreatModificat)
                {
                    Console.WriteLine($"[CREAT] {e.Path}");
                    antivirusQueue[e.Path] = DateTime.Now;
                    lock (bufferLock)
                    {
                        eventBuffer.RemoveAll(x => x.Path == e.Path);
                    }
                    break;
                }

                if (isSterCreat)
                {
                    Console.WriteLine($"[REDENUMIT] {e.OldPath} -> {e.Path}");
                    lock (bufferLock)
                    {
                        eventBuffer.RemoveAll(x => x.Path == e.Path || x.OldPath == e.OldPath);
                    }
                    break;
                }

                if (related.Count == 1)
                {
                    var evType = related[0].EventType;
                    if (evType == "CREAT")
                    {
                        Console.WriteLine($"[CREAT] {e.Path}");
                        antivirusQueue[e.Path] = DateTime.Now;
                    }
                    else if (evType == "MODIFICAT")
                    {
                        Console.WriteLine($"[MODIFICAT] {e.Path}");
                        antivirusQueue[e.Path] = DateTime.Now;
                    }
                    else if (evType == "REDENUMIT")
                    {
                        Console.WriteLine($"[REDENUMIT] {e.OldPath} -> {e.Path}");
                    }
                    else if (evType == "STERS")
                    {
                        Console.WriteLine($"[STERS] {e.Path}");
                        antivirusQueue.Remove(e.Path);

                        try
                        {
                            var lines = File.Exists("to_scan.txt")
                                ? File.ReadAllLines("to_scan.txt").ToList()
                                : new List<string>();

                            lines.RemoveAll(line => string.Equals(line.Trim(), e.Path.Trim(), StringComparison.OrdinalIgnoreCase));
                            File.WriteAllLines("to_scan.txt", lines);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[EROARE SCRIERE to_scan.txt] {ex.Message}");
                        }
                    }

                    lock (bufferLock)
                    {
                        eventBuffer.Remove(e);
                    }
                    break;
                }

                Console.WriteLine($"[{e.EventType}] {e.Path}");
                lock (bufferLock)
                {
                    eventBuffer.Remove(e);
                }
            }

            try
            {
                File.WriteAllLines("to_scan.txt", antivirusQueue.Keys);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EROARE SCRIERE ANTIVIRUS] {ex.Message}");
            }

            Thread.Sleep(200);
        }
    }
}
