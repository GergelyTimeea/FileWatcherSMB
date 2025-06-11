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
    [Fact] //Orice metodă marcată cu [Fact] este un test individual care verifică un anumit comportament.
           //Verifică dacă FileEventProcessor:
           //Trimite mesaj pentru fiecare fișier din setul de evenimente.
           //Șterge fiecare fișier după ce trimite mesajul.
    public async Task ExecuteAsync_SendsAndRemovesEvents()
    {
        //Se folosesc mock-uri (obiecte simulate) pentru a testa doar clasa,
        //  fără să depindă de implementările reale ale celorlalte clase.
        var eventSetMock = new Mock<IConcurrentHashSet>();
        var rabbitMock = new Mock<IRabbitMqProducer>();

        eventSetMock.SetupSequence(x => x.Items)
            .Returns(new[] { "file1.txt", "file2.txt" }) //Items: la prima accesare, returnează două fișiere; 
                                                         // la următoarea, nimic (ca să termine bucla).
            .Returns(Array.Empty<string>());

        eventSetMock.Setup(x => x.Remove(It.IsAny<string>())).Returns(true); //Remove: întotdeauna funcționează (returnează true).
        var mockLogger = new Mock<ILogger<FileEventProcessor>>();

        var processor = new FileEventProcessor(eventSetMock.Object, rabbitMock.Object, mockLogger.Object);
        //Creează un obiect real de tip FileEventProcessor, dar cu mock-uri la dependențe
        using var cts = new CancellationTokenSource(1000); //Pornește procesarea și oprește după 1 secundă
        await processor.StartAsync(cts.Token);

        rabbitMock.Verify(x => x.SendMessageAsync("Eveniment: file1.txt"), Times.Once);
        rabbitMock.Verify(x => x.SendMessageAsync("Eveniment: file2.txt"), Times.Once);
        eventSetMock.Verify(x => x.Remove("file1.txt"), Times.Once);
        eventSetMock.Verify(x => x.Remove("file2.txt"), Times.Once);
        //Verifică dacă metoda de trimitere a fost apelată exact o dată pentru fiecare fișier
        //  și dacă fiecare fișier a fost șters din set după trimitere.
    }

    [Fact] //Verifică dacă NU se trimite niciun mesaj dacă ștergerea fișierului din set eșuează
    public async Task ExecuteAsync_DoesNotSendIfRemoveReturnsFalse()
    {
        var eventSetMock = new Mock<IConcurrentHashSet>();
        var rabbitMock = new Mock<IRabbitMqProducer>();
        var mockLogger = new Mock<ILogger<FileEventProcessor>>();

        eventSetMock.Setup(x => x.Items).Returns(new[] { "file1.txt" }); //Items: doar un fișier.
        eventSetMock.Setup(x => x.Remove(It.IsAny<string>())).Returns(false); //Remove: mereu eșuează (returnează false).

        var processor = new FileEventProcessor(eventSetMock.Object, rabbitMock.Object, mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await processor.StartAsync(cts.Token);

        rabbitMock.Verify(x => x.SendMessageAsync(It.IsAny<string>()), Times.Never); //Verifică dacă nu s-a trimis niciun mesaj (metoda nu a fost apelată deloc).
    }
}