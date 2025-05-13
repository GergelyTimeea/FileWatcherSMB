using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MyNamespace
{
    class MyClassCS
    {
        private static readonly object eventLock = new();
        private static readonly List<(string Type, string Path, string? OldPath, DateTime Time)> eventBuffer = new();
        private static bool isRunning = true;

        static void Main()
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string? watchPath = config["NfsWatcher:WatchPath"];

            if (string.IsNullOrWhiteSpace(watchPath) || !Directory.Exists(watchPath))
            {
                Console.WriteLine($"[EROARE] Calea nu este validă sau nu există: {watchPath}");
                return;
            }

            using var watcher = new FileSystemWatcher(watchPath);

            watcher.NotifyFilter = NotifyFilters.Attributes
                                 | NotifyFilters.CreationTime
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.FileName
                                 | NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.Security
                                 | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            watcher.Filter = "*.*";
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            Thread processorThread = new(ProcessEvents);
            processorThread.Start();

            Console.WriteLine($"[INFO] Monitorizare pornită: {watchPath}");
            Console.WriteLine("Apasă Enter pentru a opri...");
            Console.ReadLine();

            isRunning = false;
            processorThread.Join();
        }

        private static bool IsTemporaryFile(string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();

            return name.StartsWith("~$") ||
                   name.StartsWith(".~lock.") ||
                   name.StartsWith(".goutputstream-") ||
                   name.EndsWith(".tmp") ||
                   name.EndsWith(".temp") ||
                   name.EndsWith(".swp") ||
                   name.EndsWith(".swx") ||
                   name == ".ds_store" ||
                   name == "thumbs.db";
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            lock (eventLock)
            {
                eventBuffer.Add(("MODIFICAT", e.FullPath, null, DateTime.Now));
            }
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            lock (eventLock)
            {
                eventBuffer.Add(("CREAT", e.FullPath, null, DateTime.Now));
            }
        }

        private static void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            lock (eventLock)
            {
                eventBuffer.Add(("STERS", e.FullPath, null, DateTime.Now));
            }
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;
            string oldName = Path.GetFileName(e.OldFullPath).ToLowerInvariant();
            if (oldName.StartsWith(".goutputstream-"))
            {
                lock (eventLock)
                {
                    eventBuffer.Add(("MODIFICAT", e.FullPath, null, DateTime.Now));
                }
                return;
            }

            lock (eventLock)
            {
                eventBuffer.Add(("REDENUMIT", e.FullPath, e.OldFullPath, DateTime.Now));
            }
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            PrintException(e.GetException());
        }

        private static void PrintException(Exception? ex)
        {
            if (ex == null) return;
            Console.WriteLine($"[EROARE] {ex.Message}");
            Console.WriteLine("Stacktrace:");
            Console.WriteLine(ex.StackTrace);
            PrintException(ex.InnerException);
        }

        private static void ProcessEvents()
        {
            while (isRunning || eventBuffer.Any())
            {
                List<(string Type, string Path, string? OldPath, DateTime Time)> toProcess;

                lock (eventLock)
                {
                    var now = DateTime.Now;
                    toProcess = eventBuffer
                        .Where(e => (now - e.Time).TotalMilliseconds > 800)
                        .ToList();

                    foreach (var e in toProcess)
                        eventBuffer.Remove(e);
                }

                var grouped = toProcess
                    .GroupBy(e => e.Path)
                    .ToList();

                foreach (var group in grouped)
                {
                    var events = group.ToList();
                    var types = events.Select(e => e.Type).ToList();

                    string result;

                    if (types.Contains("STERS") && types.Contains("CREAT") && types.Contains("MODIFICAT"))
                        result = "MODIFICAT (înlocuit)";
                    else if (types.Contains("STERS") && types.Contains("CREAT"))
                        result = "REDENUMIT (posibil mutat)";
                    else if (types.Contains("CREAT") && types.Contains("MODIFICAT"))
                        result = "CREAT + scan antivirus";
                    else if (types.Contains("REDENUMIT") && types.Contains("MODIFICAT"))
                        result = "MODIFICAT (redenumit + modificat)";
                    else if (types.Count == 1)
                        result = types[0];
                    else
                        result = string.Join(" + ", types.Distinct());

                    Console.WriteLine($"[{result}] {group.Key}");
                }

                Thread.Sleep(200);
            }
        }
    }
}
