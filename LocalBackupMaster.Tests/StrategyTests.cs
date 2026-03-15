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
}
