namespace LocalBackupMaster.Models.Backup;

public enum BackupPhase
{
    Preparing,
    Scanning,
    Copying,
    Completed,
    Canceled,
    Failed
}

public record BackupProgressReport(
    BackupPhase Phase,
    string CurrentItem = "",
    double Progress = 0,
    int ScannedCount = 0,
    int CopiedCount = 0,
    int FailedCount = 0,
    long TotalBytes = 0,
    long ProcessedBytes = 0,
    List<string>? Errors = null);
