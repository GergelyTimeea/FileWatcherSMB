using FileWatcherSMB.Helpers;
using FileWatcherSMB.Services;
using FileWatcherSMB.src.Watchers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.src.Processors
{
    public class FileEventProcessor : BackgroundService
    {
        private readonly IConcurrentHashSet _eventMap;
        private readonly IRabbitMqProducer _producer;
        private readonly ILogger<FileEventProcessor> _logger;

        public FileEventProcessor(IConcurrentHashSet eventMap, IRabbitMqProducer producer, ILogger<FileEventProcessor> logger)
        {
            _eventMap = eventMap;
            _producer = producer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    foreach (var path in _eventMap.Items)
                    {
                        try
                        {
                            await _producer.SendMessageAsync($"Eveniment: {path}");
                            _eventMap.Remove(path); // doar dacă trimiterea a mers
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Eroare la trimiterea mesajului pentru path-ul: {Path}", path);
                        }
                    }


                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }

        }

    }
}
