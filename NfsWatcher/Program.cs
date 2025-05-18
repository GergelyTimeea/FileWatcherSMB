using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;

namespace MyNamespace
{
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

        private static void AddEvent(string path)
        {
            eventMap.Add(path);
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            AddEvent(e.FullPath);
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath)) return;
            AddEvent(e.FullPath);
        }


        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (IsTemporaryFile(e.FullPath) || IsTemporaryFile(e.OldFullPath)) return;

            AddEvent(e.FullPath);
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
            while (isRunning || eventMap.Items.Any())
            {
                var paths = eventMap.Items.ToList();

                foreach (var path in paths)
                {
                    eventMap.Remove(path);

                    Console.WriteLine($"[Eveniment] {path}");
                }

                Thread.Sleep(500);
            }
        }


    }
}
