namespace LocalBackupMaster.Services;

/// <summary>C2 — Mantiene el número de versiones almacenadas dentro de un máximo configurado.</summary>
public interface IVersionCleanupService
{
    /// <summary>Directorio donde se guardan las versiones para el archivo relPath dentro de destPath.</summary>
    string GetVersionsDir(string destPath, string relPath);

    /// <summary>Nombre de archivo versionado con marca de tiempo (yyyyMMddHHmmss).</summary>
    string CreateVersionFileName(string fileName);

    /// <summary>
    /// Elimina las versiones más antiguas si se supera maxVersions.
    /// Retorna el número de versiones eliminadas.
    /// </summary>
    Task<int> CleanupAsync(string destPath, string relPath, int maxVersions, CancellationToken ct = default);
}
