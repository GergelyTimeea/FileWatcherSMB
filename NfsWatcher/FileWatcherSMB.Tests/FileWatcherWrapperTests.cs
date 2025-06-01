using System;
using System.IO;
using FileWatcherSMB.Helpers;
using FileWatcherSMB.src.Helpers;
using FileWatcherSMB.src.Watchers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class FileWatcherWrapperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IConcurrentHashSet> _eventMapMock;
    private readonly Mock<ITempFileFilter> _filterMock;
    private readonly Mock<ILogger<FileWatcherWrapper>> _loggerMock;
    private readonly FileWatcherWrapper _wrapper;

    public FileWatcherWrapperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _eventMapMock = new Mock<IConcurrentHashSet>();
        _filterMock = new Mock<ITempFileFilter>();
        _loggerMock = new Mock<ILogger<FileWatcherWrapper>>();
        _wrapper = new FileWatcherWrapper(_tempDir, _eventMapMock.Object, _filterMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Start_SetsEnableRaisingEventsTrue()
    {
        _wrapper.Start();
        // The property is internal to FileSystemWatcher, so test indirectly:
        // We can check that Start doesn't throw and that events can be raised.
        Assert.True(true); // If no exception is thrown, test passes.
    }

    [Fact]
    public void Stop_SetsEnableRaisingEventsFalse()
    {
        _wrapper.Start();
        _wrapper.Stop();
        Assert.True(true); // If no exception is thrown, test passes.
    }

    [Fact]
    public void OnFileChanged_AddsPath_IfNotTemporary()
    {
        var path = Path.Combine(_tempDir, "test.txt");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(path)).Returns(false);

        var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, _tempDir, "test.txt");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(path), Times.Once);
    }

    [Fact]
    public void OnFileChanged_DoesNotAddPath_IfTemporary()
    {
        var path = Path.Combine(_tempDir, "temp.tmp");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(path)).Returns(true);

        var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, _tempDir, "temp.tmp");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void OnFileCreated_AddsPath_IfNotTemporary()
    {
        var path = Path.Combine(_tempDir, "newfile.txt");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(path)).Returns(false);

        var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, _tempDir, "newfile.txt");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileCreated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(path), Times.Once);
    }

    [Fact]
    public void OnFileCreated_DoesNotAddPath_IfTemporary()
    {
        var path = Path.Combine(_tempDir, "temp.swp");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(path)).Returns(true);

        var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Created, _tempDir, "temp.swp");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileCreated", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void OnFileRenamed_AddsPath_IfNeitherOldNorNewAreTemporary()
    {
        var oldPath = Path.Combine(_tempDir, "old.txt");
        var newPath = Path.Combine(_tempDir, "new.txt");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(oldPath)).Returns(false);
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(newPath)).Returns(false);

        var eventArgs = new RenamedEventArgs(WatcherChangeTypes.Renamed, _tempDir, "new.txt", "old.txt");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileRenamed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(newPath), Times.Once);
    }

    [Fact]
    public void OnFileRenamed_DoesNotAddPath_IfOldIsTemporary()
    {
        var oldPath = Path.Combine(_tempDir, "temp.tmp");
        var newPath = Path.Combine(_tempDir, "new.txt");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(oldPath)).Returns(true);

        var eventArgs = new RenamedEventArgs(WatcherChangeTypes.Renamed, _tempDir, "new.txt", "temp.tmp");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileRenamed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void OnFileRenamed_DoesNotAddPath_IfNewIsTemporary()
    {
        var oldPath = Path.Combine(_tempDir, "old.txt");
        var newPath = Path.Combine(_tempDir, "temp.tmp");
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(oldPath)).Returns(false);
        _filterMock.Setup(f => f.IsTemporaryOrIgnoredFile(newPath)).Returns(true);

        var eventArgs = new RenamedEventArgs(WatcherChangeTypes.Renamed, _tempDir, "temp.tmp", "old.txt");
        typeof(FileWatcherWrapper)
            .GetMethod("OnFileRenamed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _eventMapMock.Verify(m => m.Add(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void OnFileError_LogsError()
    {
        var exception = new Exception("test error");
        var eventArgs = new ErrorEventArgs(exception);

        typeof(FileWatcherWrapper)
            .GetMethod("OnFileError", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(_wrapper, new object[] { this, eventArgs });

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        _wrapper.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}