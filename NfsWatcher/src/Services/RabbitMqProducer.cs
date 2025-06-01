using RabbitMQ.Client;
using System.Text;
using FileWatcherSMB.Models;
using Microsoft.Extensions.Logging;

namespace FileWatcherSMB.Services
{
    public class RabbitMqProducer : IRabbitMqProducer
    {
        private readonly ConnectionFactory _factory;
        private readonly string _queueName;
        private readonly ILogger<RabbitMqProducer> _logger;

        public RabbitMqProducer(RabbitMqOptions options, ILogger<RabbitMqProducer> logger)
        {
            _factory = new ConnectionFactory
            {
                HostName = options.HostName,
                UserName = options.UserName,
                Password = options.Password
            };
            _queueName = options.QueueName;
            _logger = logger;
        }

        public async Task SendMessageAsync(string message)
        {
            await using IConnection connection = await _factory.CreateConnectionAsync();
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
