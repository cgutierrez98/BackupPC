using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using LocalBackupMaster.Services.Strategies;
using Moq;
using Xunit;

namespace LocalBackupMaster.Tests;

public class StrategyTests
{
    private readonly Mock<IDatabaseService> _dbMock;
    private readonly Mock<IBackupScannerService> _scannerMock;
    private readonly IncrementalHashStrategy _strategy;

    public StrategyTests()
    {
        _dbMock = new Mock<IDatabaseService>();
        _scannerMock = new Mock<IBackupScannerService>();
        _strategy = new IncrementalHashStrategy(_dbMock.Object, _scannerMock.Object);
    }

    [Fact]
    public async Task ShouldCopy_NewFile_ReturnsTrue()
    {
        // Arrange
        var fi = new FileInfo("test.txt"); // Note: FileInfo doesn't need to exist for metadatos if we are careful
        var dest = new BackupDestination { Id = 1, BackupPath = "C:\\Backup", Uuid = "test-uuid" };
        _dbMock.Setup(d => d.GetFileRecordAsync(1, "test.txt")).ReturnsAsync((FileRecord?)null);

        // Act
        var result = await _strategy.ShouldCopyAsync(fi, "test.txt", dest, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldCopy_MetadataChanged_ReturnsTrue()
    {
        // Arrange
        // Usaremos un archivo temporal real para FileInfo para que Length y LastWriteTime funcionen
        string tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, "content");
        var fi = new FileInfo(tempPath);
        
        var dest = new BackupDestination { Id = 1, BackupPath = "C:\\Backup", Uuid = "test-uuid" };
        var record = new FileRecord 
        { 
            RelativePath = "test.txt", 
            FileSize = 0, // Diferente al real
            LastWriteTime = DateTime.MinValue 
        };
        
        _dbMock.Setup(d => d.GetFileRecordAsync(1, "test.txt")).ReturnsAsync(record);

        // Act
        var result = await _strategy.ShouldCopyAsync(fi, "test.txt", dest, CancellationToken.None);

        // Assert
        Assert.True(result);
        
        File.Delete(tempPath);
    }

    [Fact]
    public async Task ShouldCopy_DestinationMissing_ReturnsTrue()
    {
        string sourcePath = Path.GetTempFileName();
        string destinationRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destinationRoot);

        try
        {
            File.WriteAllText(sourcePath, "same-content");
            var fi = new FileInfo(sourcePath);
            var dest = new BackupDestination { Id = 7, BackupPath = destinationRoot, Uuid = "dest-1" };
            var record = new FileRecord
            {
                RelativePath = "missing.txt",
                FileSize = fi.Length,
                LastWriteTime = fi.LastWriteTimeUtc,
                FileHash = "ABC"
            };

            _dbMock.Setup(d => d.GetFileRecordAsync(dest.Id, "missing.txt")).ReturnsAsync(record);

            var result = await _strategy.ShouldCopyAsync(fi, "missing.txt", dest, CancellationToken.None);

            Assert.True(result);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ShouldCopy_UnchangedFile_ReturnsFalse()
    {
        string sourcePath = Path.GetTempFileName();
        string destinationRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string relativePath = "folder/file.txt";
        string destinationPath = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        try
        {
            File.WriteAllText(sourcePath, "stable-content");
            File.WriteAllText(destinationPath, "stable-content");

            var fi = new FileInfo(sourcePath);
            var dest = new BackupDestination { Id = 11, BackupPath = destinationRoot, Uuid = "dest-2" };
            var record = new FileRecord
            {
                RelativePath = relativePath,
                FileSize = fi.Length,
                LastWriteTime = fi.LastWriteTimeUtc,
                FileHash = "UNCHANGED"
            };

            _dbMock.Setup(d => d.GetFileRecordAsync(dest.Id, relativePath)).ReturnsAsync(record);

            var result = await _strategy.ShouldCopyAsync(fi, relativePath, dest, CancellationToken.None);

            Assert.False(result);
            _scannerMock.Verify(
                s => s.CalculateXXHash64Async(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ShouldCopy_HashMismatchAfterTimestampChange_ReturnsTrue()
    {
        string sourcePath = Path.GetTempFileName();
        string destinationRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string relativePath = "same-size.txt";
        string destinationPath = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(destinationRoot);

        try
        {
            File.WriteAllText(sourcePath, "abc");
            File.WriteAllText(destinationPath, "abc");

            var fi = new FileInfo(sourcePath);
            var olderTimestamp = fi.LastWriteTimeUtc.AddMinutes(-5);
            var dest = new BackupDestination { Id = 12, BackupPath = destinationRoot, Uuid = "dest-3" };
            var record = new FileRecord
            {
                RelativePath = relativePath,
                FileSize = fi.Length,
                LastWriteTime = olderTimestamp,
                FileHash = "OLDHASH"
            };

            _dbMock.Setup(d => d.GetFileRecordAsync(dest.Id, relativePath)).ReturnsAsync(record);
            _scannerMock.Setup(s => s.CalculateXXHash64Async(sourcePath, CancellationToken.None)).ReturnsAsync("NEWHASH");

            var result = await _strategy.ShouldCopyAsync(fi, relativePath, dest, CancellationToken.None);

            Assert.True(result);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ShouldCopy_HashMatchAfterTimestampChange_ReturnsFalse()
    {
        string sourcePath = Path.GetTempFileName();
        string destinationRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string relativePath = "same-size.txt";
        string destinationPath = Path.Combine(destinationRoot, relativePath);
        Directory.CreateDirectory(destinationRoot);

        try
        {
            File.WriteAllText(sourcePath, "abc");
            File.WriteAllText(destinationPath, "abc");

            var fi = new FileInfo(sourcePath);
            var olderTimestamp = fi.LastWriteTimeUtc.AddMinutes(-5);
            var dest = new BackupDestination { Id = 13, BackupPath = destinationRoot, Uuid = "dest-4" };
            var record = new FileRecord
            {
                RelativePath = relativePath,
                FileSize = fi.Length,
                LastWriteTime = olderTimestamp,
                FileHash = "MATCHED"
            };

            _dbMock.Setup(d => d.GetFileRecordAsync(dest.Id, relativePath)).ReturnsAsync(record);
            _scannerMock.Setup(s => s.CalculateXXHash64Async(sourcePath, CancellationToken.None)).ReturnsAsync("MATCHED");

            var result = await _strategy.ShouldCopyAsync(fi, relativePath, dest, CancellationToken.None);

            Assert.False(result);
        }
        finally
        {
            if (File.Exists(sourcePath)) File.Delete(sourcePath);
            if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, recursive: true);
        }
    }
}
