using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;

namespace MyNamespace
{
    public class ConcurrentHashSet
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new();
        public bool Add(string item) => _dict.TryAdd(item, 0);
        public bool Contains(string item) => _dict.ContainsKey(item);
        public IEnumerable<string> Items => _dict.Keys;
    }
    class MyClassCS
    {
        private static readonly ConcurrentHashSet eventMap = new();
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

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\n[INFO] Ctrl+C detectat. Închidere...");
                e.Cancel = true;
                GracefulExit(processorThread);
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                Console.WriteLine("\n[INFO] Aplicația se închide. Închidere...");
                GracefulExit(processorThread);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.WriteLine("\n[EROARE] Excepție neașteptată:");
                if (e.ExceptionObject is Exception ex)
                    Console.WriteLine(ex.Message);
                GracefulExit(processorThread);
            };

            Console.WriteLine($"[INFO] Monitorizare pornită: {watchPath}");
            Console.WriteLine("Apasă Enter pentru a opri...");
            Console.ReadLine();

            GracefulExit(processorThread);
        }

        static void GracefulExit(Thread processorThread)
        {
            isRunning = false;
            processorThread.Join();
            Console.WriteLine("[INFO] Thread-ul de procesare s-a încheiat.");
            Environment.Exit(0);
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

        private static void AddEvent(string path, string type)
        {
            var set = eventMap.Add(path);
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            AddEvent(e.FullPath, "MODIFICAT");
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            AddEvent(e.FullPath, "CREAT");
        }

        private static void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            AddEvent(e.FullPath, "STERS");
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;

            string oldName = Path.GetFileName(e.OldFullPath).ToLowerInvariant();
            if (oldName.StartsWith(".goutputstream-"))
            {
                AddEvent(e.FullPath, "MODIFICAT");
                return;
            }

            AddEvent(e.FullPath, "REDENUMIT");
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

            // stop process events task
        }

        private static void ProcessEvents()
        {
            while (isRunning || !eventMap.IsEmpty)
            {
                var paths = eventMap.Keys.ToList(); // snapshot

                foreach (var path in paths)
                {
                    if (eventMap.TryRemove(path, out var typesSet))
                    {
                        var types = typesSet.Items.ToList();
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

                        Console.WriteLine($"[{result}] {path}");
                    }
                }

                Thread.Sleep(500);
            }
        }
    }
}
