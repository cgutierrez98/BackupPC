using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;

namespace LocalBackupMaster.Services;

/// <summary>B2 — Verifica la integridad de los archivos en destino contra el catálogo de hashes.</summary>
public class IntegrityCheckService : IIntegrityCheckService
{
    private readonly IDatabaseService         _db;
    private readonly IBackupScannerService    _scanner;

    public IntegrityCheckService(IDatabaseService db, IBackupScannerService scanner)
    {
        _db      = db;
        _scanner = scanner;
    }

    public async Task<List<IntegrityIssue>> VerifyDestinationAsync(
        BackupDestination destination,
        IProgress<BackupProgressReport>? progress = null,
        CancellationToken ct = default)
    {
        var issues = new List<IntegrityIssue>();

        if (string.IsNullOrEmpty(destination.BackupPath) || !Directory.Exists(destination.BackupPath))
        {
            issues.Add(new IntegrityIssue("", null, null, "El directorio de destino no existe."));
            return issues;
        }

        var records = await _db.GetFileRecordsByDestinationAsync(destination.Id);
        int total   = records.Count;
        int done    = 0;

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();

            done++;
            var fullPath = Path.Combine(destination.BackupPath, record.RelativePath);

            if (!File.Exists(fullPath))
            {
                issues.Add(new IntegrityIssue(record.RelativePath, record.FileHash, null, "Archivo ausente en destino."));
            }
            else if (record.FileHash is not null)
            {
                // No descifrar — comparamos el hash del archivo tal como está en disco
                var actualHash = await _scanner.CalculateXXHash64Async(fullPath, ct);
                if (actualHash != record.FileHash)
                    issues.Add(new IntegrityIssue(record.RelativePath, record.FileHash, actualHash,
                        "Hash no coincide — posible corrupción de datos."));
            }

            progress?.Report(new BackupProgressReport(
                BackupPhase.Scanning,
                record.RelativePath,
                total > 0 ? (double)done / total : 0,
                done, 0, issues.Count));
        }

        progress?.Report(new BackupProgressReport(BackupPhase.Completed,
            $"{done} archivos verificados — {issues.Count} problema(s) encontrado(s)."));

        return issues;
    }
}
