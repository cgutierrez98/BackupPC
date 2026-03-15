using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace LocalBackupMaster.Tests;

public class ExportTests
{
    private readonly JsonReportExportService _exportService;

    public ExportTests()
    {
        _exportService = new JsonReportExportService();
    }

    [Fact]
    public async Task ExportToJson_CreatesValidFile()
    {
        // Arrange
        var report = new BackupReport
        {
            StartedAt = DateTime.Now.AddMinutes(-5),
            FinishedAt = DateTime.Now,
            TotalScanned = 100,
            TotalCopied = 80,
            TotalFailed = 5,
            TotalSkipped = 15,
            TotalBytesCopied = 1024 * 1024,
            ParallelDegree = 4,
            FailedFiles = new() { "error.txt: Locked" },
            CopiedFiles = new() { "doc.pdf", "image.png" }
        };

        string tempFile = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.json");

        try
        {
            // Act
            var path = await _exportService.ExportToJsonAsync(report, tempFile);

            // Assert
            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            var deserialized = JsonSerializer.Deserialize<BackupReport>(content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            Assert.NotNull(deserialized);
            Assert.Equal(report.TotalScanned, deserialized.TotalScanned);
            Assert.Equal(report.TotalCopied, deserialized.TotalCopied);
            Assert.Equal(report.TotalBytesCopied, deserialized.TotalBytesCopied);
            Assert.Equal(report.CopiedFiles.Count, deserialized.CopiedFiles.Count);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
