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

    [Fact]
    public async Task GetTotalFilesAndSizeAsync_RespectsIgnorePatterns()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, ".backupignore"), "*.tmp");

            string includedFile = Path.Combine(root, "keep.txt");
            string ignoredFile = Path.Combine(root, "ignore.tmp");
            string blacklistedDir = Path.Combine(root, "bin");
            Directory.CreateDirectory(blacklistedDir);
            string blacklistedFile = Path.Combine(blacklistedDir, "ignored.dll");

            await File.WriteAllTextAsync(includedFile, "12345");
            await File.WriteAllTextAsync(ignoredFile, "123456789");
            await File.WriteAllTextAsync(blacklistedFile, "987654321");

            var service = new BackupScannerService();

            var (totalFiles, totalBytes) = await service.GetTotalFilesAndSizeAsync([root], CancellationToken.None);

            Assert.Equal(1, totalFiles);
            Assert.Equal(new FileInfo(includedFile).Length, totalBytes);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CalculateXXHash64Async_ReturnsStableNonEmptyHash()
    {
        string filePath = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(filePath, "contenido estable");
            var service = new BackupScannerService();

            string firstHash = await service.CalculateXXHash64Async(filePath, CancellationToken.None);
            string secondHash = await service.CalculateXXHash64Async(filePath, CancellationToken.None);

            Assert.False(string.IsNullOrWhiteSpace(firstHash));
            Assert.Equal(firstHash, secondHash);
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Fact]
    public async Task CalculateXXHash64Async_MissingFile_ReturnsEmptyString()
    {
        var service = new BackupScannerService();

        var hash = await service.CalculateXXHash64Async(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.missing"), CancellationToken.None);

        Assert.Equal(string.Empty, hash);
    }
}
