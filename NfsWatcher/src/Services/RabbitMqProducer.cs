using FileWatcherSMB.Models;
using FileWatcherSMB.src.Services;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace FileWatcherSMB.Services
{
    public class RabbitMqProducer : IRabbitMqProducer
    {
        private readonly IConnectionFactoryWrapper _factoryWrapper;
        private readonly string _queueName;
        private readonly ILogger<RabbitMqProducer> _logger;

        public RabbitMqProducer(IConnectionFactoryWrapper factoryWrapper, RabbitMqOptions options, ILogger<RabbitMqProducer> logger)
        {
            _factoryWrapper = factoryWrapper;
            _queueName = options.QueueName;
            _logger = logger;
        }

        public async Task SendMessageAsync(string message)
        {
            await using IConnection connection = await _factoryWrapper.CreateConnectionAsync();
            await using IChannel channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: _queueName,
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: false,
                                            arguments: null);

            var body = Encoding.UTF8.GetBytes(message);
            var props = new BasicProperties();

            await channel.BasicPublishAsync("", _queueName, false, props, body);
            _logger.LogInformation("[x] Mesaj trimis: {Message}", message);
        }
    }
}
