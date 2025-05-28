using RabbitMQ.Client;
using System.Text;

namespace FileWatcherSMB.Services
{
    public class RabbitMqProducer : IRabbitMqProducer
    {
        private readonly ConnectionFactory _factory;
        private readonly string _queueName;

        public RabbitMqProducer(RabbitMqOptions options)
        {
            _factory = new ConnectionFactory
            {
                HostName = options.HostName,
                UserName = options.UserName,
                Password = options.Password
            };
            _queueName = options.QueueName;
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
            Console.WriteLine($"[x] Mesaj trimis: {message}");
        }
    }
}