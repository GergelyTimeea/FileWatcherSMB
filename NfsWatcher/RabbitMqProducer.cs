using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyNamespace
{
    public class RabbitMqProducer
    {
        private readonly ConnectionFactory _factory;
        private readonly string _queueName;

        public RabbitMqProducer(RabbitMqOptions settings)
        {
            _factory = new ConnectionFactory
            {
                HostName = settings.HostName,
                UserName = settings.UserName,
                Password = settings.Password
            };
            _queueName = settings.QueueName;
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

            await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: _queueName,
                            mandatory: false,
                            basicProperties: props,
                            body: body);

            Console.WriteLine($"[x] Mesaj trimis: {message}");
        }
    }
}
