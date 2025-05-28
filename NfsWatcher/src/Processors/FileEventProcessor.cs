using FileWatcherSMB.Helpers;
using FileWatcherSMB.Services;
using Microsoft.Extensions.Hosting;
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

        public FileEventProcessor(IConcurrentHashSet eventMap, IRabbitMqProducer producer)
        {
            _eventMap = eventMap;
            _producer = producer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var paths = _eventMap.Items.ToList();
                    foreach (var path in paths)
                    {
                        if (_eventMap.Remove(path))
                        {
                            await _producer.SendMessageAsync($"Eveniment: {path}");
                        }
                    }

                    await Task.Delay(500, stoppingToken);
                }
            }
            catch (OperationCanceledException) { }

        }

    }
}
