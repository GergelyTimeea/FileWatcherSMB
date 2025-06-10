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

            var mockChannel = new Mock<IChannel>();

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

            var mockConnection = new Mock<IConnection>();
            
            mockConnection
                .Setup(c => c.CreateChannelAsync(
                    It.IsAny<CreateChannelOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockChannel.Object);

            var mockFactoryWrapper = new Mock<IConnectionFactoryWrapper>();

            mockFactoryWrapper
                .Setup(f => f.CreateConnectionAsync())
                .ReturnsAsync(mockConnection.Object);

            var producer = new RabbitMqProducer(mockFactoryWrapper.Object, rabbitOptions, mockLogger.Object);

            await producer.SendMessageAsync(testMessage);

            mockFactoryWrapper.Verify(f => f.CreateConnectionAsync(), Times.Once);


            mockConnection.Verify(c => c.CreateChannelAsync(
                It.IsAny<CreateChannelOptions>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            mockChannel.Verify(c => c.QueueDeclareAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
  
            mockChannel.Verify(c =>
                c.BasicPublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<BasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<System.Threading.CancellationToken>()
                ), Times.Once
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
