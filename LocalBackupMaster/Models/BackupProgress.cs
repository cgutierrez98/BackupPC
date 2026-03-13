namespace LocalBackupMaster.Models;

public record BackupProgress(
    string CurrentFileName,
    double ProgressValue,
    string PercentageText,
    int ScannedCount,
    int CopiedCount,
    int FailedCount,
    bool IsPreScanning = false);
