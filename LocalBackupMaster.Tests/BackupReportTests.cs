using LocalBackupMaster.Models;

namespace LocalBackupMaster.Tests;

public class BackupReportTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1_048_576, "1.0 MB")]
    [InlineData(1_073_741_824, "1.0 GB")]
    public void SizeText_FormatsBoundaryValues(long bytes, string expected)
    {
        var report = new BackupReport { TotalBytesCopied = bytes };

        Assert.Equal(expected, report.SizeText);
    }

    [Theory]
    [InlineData(59, "59s")]
    [InlineData(60, "1m 0s")]
    [InlineData(125, "2m 5s")]
    public void DurationText_FormatsShortAndLongDurations(int seconds, string expected)
    {
        var report = new BackupReport
        {
            StartedAt = DateTime.UnixEpoch,
            FinishedAt = DateTime.UnixEpoch.AddSeconds(seconds)
        };

        Assert.Equal(expected, report.DurationText);
    }

    [Fact]
    public void ResultHelpers_PrioritizeDryRunOverFailures()
    {
        var report = new BackupReport
        {
            WasDryRun = true,
            TotalFailed = 3
        };

        Assert.Equal("🔍", report.ResultEmoji);
        Assert.Equal("Simulacro completado", report.ResultText);
    }

    [Fact]
    public void ResultHelpers_ReturnWarningWhenFailuresExist()
    {
        var report = new BackupReport { TotalFailed = 1 };

        Assert.Equal("⚠️", report.ResultEmoji);
        Assert.Equal("Completado con advertencias", report.ResultText);
    }

    [Fact]
    public void ResultHelpers_ReturnSuccessWhenNoFailuresExist()
    {
        var report = new BackupReport { TotalFailed = 0 };

        Assert.Equal("✅", report.ResultEmoji);
        Assert.Equal("Completado exitosamente", report.ResultText);
    }
}
