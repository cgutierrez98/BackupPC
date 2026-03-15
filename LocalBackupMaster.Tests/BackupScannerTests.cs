using LocalBackupMaster.Services;
using Xunit;

namespace LocalBackupMaster.Tests;

public class BackupScannerTests
{
    [Theory]
    [InlineData(".git/config", true)]
    [InlineData("node_modules/package.json", true)]
    [InlineData("bin/Debug/net9.0/app.exe", true)]
    [InlineData("obj/project.assets.json", true)]
    [InlineData("src/main.cs", false)]
    [InlineData("Documents/backup.zip", false)]
    public void IsIgnored_DefaultBlacklist_Works(string path, bool expected)
    {
        // Act
        bool result = BackupScannerService.IsIgnored(path, new HashSet<string>());

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsIgnored_CustomPatterns_Works()
    {
        // Arrange
        var patterns = new HashSet<string> { "*.tmp", "secrets.txt" };
        
        // Act & Assert
        Assert.True(BackupScannerService.IsIgnored("temp/file.tmp", patterns));
        Assert.True(BackupScannerService.IsIgnored("data/secrets.txt", patterns));
        Assert.False(BackupScannerService.IsIgnored("data/readme.txt", patterns));
    }
}
