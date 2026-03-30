using LocalBackupMaster.Models;

namespace LocalBackupMaster.Services;

/// <summary>D3 — Implementación de restauración inteligente.</summary>
public class RestoreService : IRestoreService
{
    private readonly IDatabaseService  _db;
    private readonly IEncryptionService _encryption;

    public RestoreService(IDatabaseService db, IEncryptionService encryption)
    {
        _db         = db;
        _encryption = encryption;
    }

    public Task<List<FileRecord>> GetFileTreeAsync(BackupDestination destination)
        => _db.GetFileRecordsByDestinationAsync(destination.Id);

    public async Task<int> RestoreFilesAsync(
        BackupDestination destination,
        IEnumerable<FileRecord> files,
        string outputDirectory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(destination.BackupPath))
            throw new InvalidOperationException("El destino no tiene ruta de backup configurada.");

        Directory.CreateDirectory(outputDirectory);
        int restored = 0;

        foreach (var record in files)
        {
            ct.ThrowIfCancellationRequested();

            var srcPath = Path.Combine(destination.BackupPath, record.RelativePath);
            if (!File.Exists(srcPath)) continue;

            var dstPath = Path.Combine(outputDirectory, record.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);

            if (record.IsEncrypted)
                await _encryption.DecryptFileAsync(srcPath, dstPath, destination.Uuid, ct);
            else
                File.Copy(srcPath, dstPath, overwrite: true);

            restored++;
        }

        return restored;
    }
}
