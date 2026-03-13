using LocalBackupMaster.Models;

namespace LocalBackupMaster.Services.Strategies;

public interface IBackupStrategy
{
    Task<bool> ShouldCopyAsync(FileInfo fileInfo, string relativePath, BackupDestination destination, CancellationToken token);
}
