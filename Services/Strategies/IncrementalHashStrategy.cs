using LocalBackupMaster.Models;
using LocalBackupMaster.Services;

namespace LocalBackupMaster.Services.Strategies;

public class IncrementalHashStrategy : IBackupStrategy
{
    private readonly IDatabaseService _databaseService;
    private readonly IBackupScannerService _scannerService;

    public IncrementalHashStrategy(IDatabaseService databaseService, IBackupScannerService scannerService)
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

        // 2. Sin registro previo o destino ausente → siempre copiar
        if (existing is null || !dstExists)
            return true;

        // 3. Tamaño diferente → archivos claramente distintos, sin necesidad de hash
        if (existing.FileSize != fi.Length)
            return true;

        // 4. Mismo tamaño pero mtime cambió → verificar con Hash para detectar modificaciones silenciosas
        if (existing.LastWriteTime < fi.LastWriteTimeUtc)
        {
            string currentHash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
            return currentHash != existing.FileHash;
        }

        return false;
    }
}
