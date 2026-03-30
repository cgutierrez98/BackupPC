using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;

namespace LocalBackupMaster.Services;

/// <summary>Resultado de un issue de integridad encontrado por <see cref="IIntegrityCheckService"/>.</summary>
public record IntegrityIssue(
    string RelativePath,
    string? StoredHash,
    string? ActualHash,
    string  Reason);

/// <summary>B2 — Detección de bit-rot: compara hashes almacenados vs archivos reales en destino.</summary>
public interface IIntegrityCheckService
{
    Task<List<IntegrityIssue>> VerifyDestinationAsync(
        BackupDestination destination,
        IProgress<BackupProgressReport>? progress = null,
        CancellationToken ct = default);
}
