using FileWatcherSMB.Models;
using FileWatcherSMB.Services;
using FileWatcherSMB.src.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileWatcherSMB.Tests
{
    public class RabbitMqProducerTests
    {
        [Fact]
        public async Task SendMessageAsync_PublishesMessageToRabbitMQ()
        {
            var queueName = "test-queue";
            var testMessage = "Acesta este un mesaj de test";

            var rabbitOptions = new RabbitMqOptions
            {
                QueueName = queueName,
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            var mockLogger = new Mock<ILogger<RabbitMqProducer>>(); 
            //Mock pentru canalul RabbitMQ (simulează operațiunile pe canal).

            var mockChannel = new Mock<IChannel>(); //Mock pentru canalul RabbitMQ (simulează operațiunile pe canal).

            mockChannel
                .Setup(c => c.QueueDeclareAsync(
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),  
                    It.IsAny<IDictionary<string, object?>>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new QueueDeclareOk(queueName, 0, 0));
           
            mockChannel
                .Setup(c => c.BasicPublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<BasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            var mockConnection = new Mock<IConnection>(); //
            
            mockConnection
                .Setup(c => c.CreateChannelAsync(
                    It.IsAny<CreateChannelOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockChannel.Object);
                //Mock pentru conexiunea RabbitMQ (simulează deschiderea canalului).

            var mockFactoryWrapper = new Mock<IConnectionFactoryWrapper>();
            //Mock pentru factory-ul care creează conexiunea RabbitMQ.

            mockFactoryWrapper
                .Setup(f => f.CreateConnectionAsync()) 
                .ReturnsAsync(mockConnection.Object);

            var producer = new RabbitMqProducer(mockFactoryWrapper.Object, rabbitOptions, mockLogger.Object);

            await producer.SendMessageAsync(testMessage);

            mockFactoryWrapper.Verify(f => f.CreateConnectionAsync(), Times.Once); //Simulează crearea unei conexiuni RabbitMQ.


            mockConnection.Verify(c => c.CreateChannelAsync(
                It.IsAny<CreateChannelOptions>(),
                It.IsAny<CancellationToken>()),
                Times.Once); //Simulează crearea unui canal pe conexiunea RabbitMQ.

            mockChannel.Verify(c => c.QueueDeclareAsync( 
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
                Times.Once); // Simulează declararea unei cozi în RabbitMQ. 
                // Va returna mereu cu succes, ca și cum coada a fost creată.
  
            mockChannel.Verify(c =>
                c.BasicPublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<BasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<System.Threading.CancellationToken>()
                ), Times.Once //Simulează publicarea mesajului pe coadă. Va returna mereu cu succes.
            );

            mockLogger.Verify(logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString().Contains($"[x] Mesaj trimis: {testMessage}")),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Once);
        }
    }
}
