using FileWatcherSMB.Helpers;
using FileWatcherSMB.Services;
using FileWatcherSMB.src.Processors;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FileWatcherSMB.Tests;

public class FileEventProcessorTests
{
    [Fact]
    public async Task ExecuteAsync_SendsAndRemovesEvents()
    {
        var eventSetMock = new Mock<IConcurrentHashSet>();
        var rabbitMock = new Mock<IRabbitMqProducer>();

        eventSetMock.SetupSequence(x => x.Items)
            .Returns(new[] { "file1.txt", "file2.txt" })
            .Returns(Array.Empty<string>());

        eventSetMock.Setup(x => x.Remove(It.IsAny<string>())).Returns(true);
        var mockLogger = new Mock<ILogger<FileEventProcessor>>();

        var processor = new FileEventProcessor(eventSetMock.Object, rabbitMock.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource(1000);
        await processor.StartAsync(cts.Token);

        rabbitMock.Verify(x => x.SendMessageAsync("Eveniment: file1.txt"), Times.Once);
        rabbitMock.Verify(x => x.SendMessageAsync("Eveniment: file2.txt"), Times.Once);
        eventSetMock.Verify(x => x.Remove("file1.txt"), Times.Once);
        eventSetMock.Verify(x => x.Remove("file2.txt"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSendIfRemoveReturnsFalse()
    {
        var eventSetMock = new Mock<IConcurrentHashSet>();
        var rabbitMock = new Mock<IRabbitMqProducer>();
        var mockLogger = new Mock<ILogger<FileEventProcessor>>();

        eventSetMock.Setup(x => x.Items).Returns(new[] { "file1.txt" });
        eventSetMock.Setup(x => x.Remove(It.IsAny<string>())).Returns(false);

        var processor = new FileEventProcessor(eventSetMock.Object, rabbitMock.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await processor.StartAsync(cts.Token);

        rabbitMock.Verify(x => x.SendMessageAsync(It.IsAny<string>()), Times.Never);
    }
}