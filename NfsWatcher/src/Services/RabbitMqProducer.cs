using FileWatcherSMB.Models;
using FileWatcherSMB.src.Services;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;

namespace FileWatcherSMB.Services
{
    public class RabbitMqProducer : IRabbitMqProducer
    {
        private readonly IConnectionFactoryWrapper _factoryWrapper; //Creează conexiuni către serverul RabbitMQ
        private readonly string _queueName; //Numele cozii la care se va trimite mesajul
        private readonly ILogger<RabbitMqProducer> _logger; //Logger pentru logarea operațiilor și a eventualelor erori.

        public RabbitMqProducer(IConnectionFactoryWrapper factoryWrapper, RabbitMqOptions options, ILogger<RabbitMqProducer> logger)
        { //constructor primește toate dependențele prin injectare
            _factoryWrapper = factoryWrapper;
            _queueName = options.QueueName;
            _logger = logger;
        }

        public async Task SendMessageAsync(string message)
        {
            await using IConnection connection = await _factoryWrapper.CreateConnectionAsync(); //Creează o conexiune către RabbitMQ 
            await using IChannel channel = await connection.CreateChannelAsync(); // Creează un canal de comunicație 

            await channel.QueueDeclareAsync(queue: _queueName,
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: false,
                                            arguments: null); //Declară coada dacă nu există deja

            var body = Encoding.UTF8.GetBytes(message); //Transformă mesajul într-un array de bytes 
            var props = new BasicProperties(); 

            await channel.BasicPublishAsync("", _queueName, false, props, body);
            _logger.LogInformation("[x] Mesaj trimis: {Message}", message); // Loghează mesajul trimis
        }
    }
}
//Async = Nu blochează firul principal și permite trimiterea rapidă a multor mesaje, util mai ales la trafic mare.