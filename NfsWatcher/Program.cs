using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace MyNamespace
{
    class MyClassCS
    {
        private static readonly ConcurrentHashSet eventMap = new();
        private static bool isRunning = true;
        private static TempFileFilter? _tempFileFilter;

        static void Main()
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var rabbitSettings = config
                .GetSection("RabbitMq")
                .Get<RabbitMqOptions>()
                ?? throw new InvalidOperationException("Lip­seste sectiunea RabbitMq în appsettings.json");

            var producer = new RabbitMqProducer(rabbitSettings);

            string? watchPath = config["NfsWatcher:WatchPath"];

            if (string.IsNullOrWhiteSpace(watchPath) || !Directory.Exists(watchPath))
            {
                Console.WriteLine($"[EROARE] Calea nu este validă sau nu există: {watchPath}");
                return;
            }

            // Load regex ignore patterns
            var ignorePatterns = config.GetSection("NfsWatcher:IgnorePatterns").Get<List<string>>() ?? new();
            _tempFileFilter = new TempFileFilter(ignorePatterns);

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

            Thread processorThread = new(() => ProcessEvents(producer));
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

        private static void AddEvent(string path)
        {
            eventMap.Add(path);
        }

        private static bool ShouldIgnore(string path)
        {
            return _tempFileFilter?.IsTemporary(path) ?? false;
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (ShouldIgnore(e.FullPath)) return;
            AddEvent(e.FullPath);
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (ShouldIgnore(e.FullPath)) return;
            AddEvent(e.FullPath);
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (ShouldIgnore(e.FullPath) || ShouldIgnore(e.OldFullPath)) return;
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
        }

        private static void ProcessEvents(RabbitMqProducer producer)
        {
            while (isRunning || eventMap.Items.Any())
            {
                var paths = eventMap.Items.ToList();

                foreach (var path in paths)
                {
                    eventMap.Remove(path);
                    Console.WriteLine($"[Eveniment] {path}");
                    _ = producer.SendMessageAsync($"Eveniment: {path}");
                }

                Thread.Sleep(500);
            }
        }
    }

    public class TempFileFilter
    {
        private readonly List<Regex> _ignoreRegexes;

        public TempFileFilter(IEnumerable<string> patterns)
        {
            _ignoreRegexes = patterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                .ToList();
        }

        public bool IsTemporary(string path)
        {
            string fileName = Path.GetFileName(path);
            return _ignoreRegexes.Any(regex => regex.IsMatch(fileName));
        }
    }
}
