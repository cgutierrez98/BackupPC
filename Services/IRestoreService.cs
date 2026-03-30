using LocalBackupMaster.Models;

namespace LocalBackupMaster.Services;

/// <summary>D3 — Restauración inteligente de archivos desde el destino al sistema local.</summary>
public interface IRestoreService
{
    /// <summary>Retorna todos los FileRecords catalogados para el destino.</summary>
    Task<List<FileRecord>> GetFileTreeAsync(BackupDestination destination);

    /// <summary>
    /// Restaura los archivos indicados en outputDirectory, descifrando si es necesario.
    /// Devuelve el número de archivos restaurados.
    /// </summary>
    Task<int> RestoreFilesAsync(
        BackupDestination destination,
        IEnumerable<FileRecord> files,
        string outputDirectory,
        CancellationToken ct = default);
}
