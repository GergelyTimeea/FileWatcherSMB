using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using FileWatcherSMB.Helpers;
using FileWatcherSMB.Models;
using FileWatcherSMB.Services;

namespace FileWatcherSMB
{
    class Program
    {
        private static IConcurrentHashSet eventMap = new ConcurrentHashSet();
        private static bool isRunning = true;
        private static IRabbitMqProducer producer;

        static void Main()
        {
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var rabbitSettings = config
                .GetSection("RabbitMq")
                .Get<RabbitMqOptions>() ?? throw new InvalidOperationException("Lipsește RabbitMq din config");

            producer = new RabbitMqProducer(rabbitSettings);

            string? watchPath = config["NfsWatcher:WatchPath"];

            if (string.IsNullOrWhiteSpace(watchPath) || !Directory.Exists(watchPath))
            {
                Console.WriteLine($"[EROARE] Calea nu este validă: {watchPath}");
                return;
            }

            using var watcher = new FileSystemWatcher(watchPath)
            {
                NotifyFilter = NotifyFilters.Attributes
                             | NotifyFilters.CreationTime
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.FileName
                             | NotifyFilters.LastAccess
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Security
                             | NotifyFilters.Size,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;

            var processorThread = new Thread(ProcessEvents);
            processorThread.Start();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                GracefulExit(processorThread);
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) => GracefulExit(processorThread);

            Console.WriteLine($"[INFO] Monitorizare pornită: {watchPath}");
            Console.ReadLine();
            GracefulExit(processorThread);
        }

        private static void GracefulExit(Thread processorThread)
        {
            isRunning = false;
            processorThread.Join();
            Console.WriteLine("[INFO] Monitorizarea s-a încheiat.");
            Environment.Exit(0);
        }

        private static readonly Regex tempFileRegex = new(
            "(^~\\$|\\.tmp$|\\.temp$|\\.swp$|\\.swx$|\\.ds_store$|thumbs\\.db$|^\\.~lock\\.|^\\.goutputstream-)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool IsTemporaryFile(string path)
        {
            string name = Path.GetFileName(path);
            return tempFileRegex.IsMatch(name);
        }

        private static void AddEvent(string path)
        {
            eventMap.Add(path);
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (!IsTemporaryFile(e.FullPath)) AddEvent(e.FullPath);
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (!IsTemporaryFile(e.FullPath)) AddEvent(e.FullPath);
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsTemporaryFile(e.FullPath)) AddEvent(e.FullPath);
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"[EROARE] {e.GetException()?.Message}");
        }

        private static async void ProcessEvents()
        {
            while (isRunning || eventMap.Items.Any())
            {
                var paths = eventMap.Items.ToList();
                foreach (var path in paths)
                {
                    eventMap.Remove(path);
                    Console.WriteLine($"[Eveniment] {path}");
                    await producer.SendMessageAsync($"Eveniment: {path}");
                }
                Thread.Sleep(500);
            }
        }
    }
}