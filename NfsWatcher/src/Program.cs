using FileWatcherSMB.Helpers;
using FileWatcherSMB.Models;
using FileWatcherSMB.Services;
using FileWatcherSMB.src.Helpers;
using FileWatcherSMB.src.Processors;
using FileWatcherSMB.src.Watchers;
using FileWatcherSMB.Watchers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;

namespace FileWatcherSMB
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);

                    logging.AddFilter("Microsoft", LogLevel.Warning);
                })
                .ConfigureAppConfiguration((context, cfg) =>
                {
                    cfg.SetBasePath(Directory.GetCurrentDirectory())
                       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                       .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;

                    var ignorePatterns = config
                        .GetSection("NfsWatcher:IgnorePatterns")
                        .Get<List<string>>() ?? new List<string>();

                    services.AddSingleton<ITempFileFilter>(new TempFileFilter(ignorePatterns));

                    services.AddSingleton<IConcurrentHashSet, ConcurrentHashSet>();

                    services.AddSingleton<IFileWatcher>(sp =>
                    {
                        var path = config["NfsWatcher:WatchPath"];
                        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                        {
                            throw new InvalidOperationException($"Calea nu este validă: {path}");
                        }
                        return new FileWatcherWrapper(path, sp.GetRequiredService<IConcurrentHashSet>(), sp.GetRequiredService<ITempFileFilter>());
                    });

                    services.AddSingleton<IRabbitMqProducer>(sp =>
                    {
                        var rabbitSettings = config
                            .GetSection("RabbitMq")
                            .Get<RabbitMqOptions>()
                            ?? throw new InvalidOperationException("Lipsește RabbitMq din config");
                        return new RabbitMqProducer(rabbitSettings);
                    });

                    services.AddHostedService<FileEventProcessor>();
                })
                .Build();

            var watcher = host.Services.GetRequiredService<IFileWatcher>();
            watcher.Start();
            Console.WriteLine($"[INFO] Monitorizare pornită.");

            await host.RunAsync();

            watcher.Stop();
            Console.WriteLine("[INFO] Monitorizarea s-a încheiat.");

        }
    }
}
