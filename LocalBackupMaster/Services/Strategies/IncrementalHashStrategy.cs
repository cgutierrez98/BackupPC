using LocalBackupMaster.Models;
using LocalBackupMaster.Services;

namespace LocalBackupMaster.Services.Strategies;

public class IncrementalHashStrategy : IBackupStrategy
{
    private readonly DatabaseService _databaseService;
    private readonly BackupScannerService _scannerService;

    public IncrementalHashStrategy(DatabaseService databaseService, BackupScannerService scannerService)
    {
        _databaseService = databaseService;
        _scannerService = scannerService;
    }

    public async Task<bool> ShouldCopyAsync(FileInfo fi, string relPath, BackupDestination dest, CancellationToken token)
    {
        string dstPath = Path.Combine(dest.BackupPath!, relPath);
        bool dstExists = File.Exists(dstPath);

        // 1. Obtener registro del catálogo
        FileRecord? existing = await _databaseService.GetFileRecordAsync(dest.Id, relPath);

        // 2. Lógica incremental básica (Metadatos)
        bool needsCopy = existing is null 
                      || !dstExists 
                      || existing.FileSize != fi.Length 
                      || existing.LastWriteTime < fi.LastWriteTimeUtc;

        // 3. Validación profunda (Hash) si hay dudas pero el archivo existe
        if (needsCopy && existing is not null && dstExists)
        {
            string currentHash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
            needsCopy = currentHash != existing.FileHash;
        }

        return needsCopy;
    }
}
