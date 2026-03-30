using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;
using LocalBackupMaster.Services.Strategies;

namespace LocalBackupMaster.Services.BackupEngine;

public interface IBackupEngine
{
    Task<BackupReport> ExecuteAsync(
        IEnumerable<BackupSource>          sources,
        BackupDestination                  destination,
        IBackupStrategy                    strategy,
        int                                parallelDegree,
        IProgress<BackupProgressReport>    progress,
        CancellationToken                  token,
        IEnumerable<string>?               includeExtensions = null,
        bool                               dryRun            = false,
        DateTimeOffset?                    sinceDate         = null);
}
