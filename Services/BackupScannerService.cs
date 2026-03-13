using System;
using System.IO;
using System.IO.Hashing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

/// <summary>
/// Resultado del intento de copia de un archivo. Indica si tuvo éxito o fue omitido.
/// </summary>
public class FileProcessResult
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailReason { get; set; }
}

public class BackupScannerService
{
    private const string IgnoreFileName = ".backupignore";
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;

    // ────────────────────────────────────────────────
    //  .backupignore helpers
    // ────────────────────────────────────────────────

    /// <summary>
    /// Lee los patrones del archivo .backupignore en basePath (si existe).
    /// Cada línea no vacía y que no comience con '#' es un patrón glob simple.
    /// </summary>
    private static HashSet<string> LoadIgnorePatterns(string basePath)
    {
        var ignoreFile = Path.Combine(basePath, IgnoreFileName);
        if (!File.Exists(ignoreFile))
            return [];

        return File.ReadAllLines(ignoreFile)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .Select(l => l.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Comprueba si la ruta relativa de un archivo coincide con algún patrón de exclusión.
    /// Soporta patrones con comodines simples: * y **.
    /// </summary>
    private static bool IsIgnored(string relativePath, HashSet<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            // Comprobación de directorio: si el patrón no tiene *, compara segmentos
            var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => s.Equals(pattern.TrimEnd('/').TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                return true;

            // Comprobación por extensión o glob simple (ej. *.bin, *.tmp)
            if (pattern.StartsWith("*.") &&
                relativePath.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ────────────────────────────────────────────────
    //  Pre-escaneo real de bytes y archivos
    // ────────────────────────────────────────────────

    /// <summary>
    /// Realiza un pre-escaneo rápido para conocer el total exacto de bytes y archivos
    /// que luego procesará el backup, respetando las reglas .backupignore.
    /// </summary>
    public async Task<(int TotalFiles, long TotalBytes)> GetTotalFilesAndSizeAsync(
        IEnumerable<string> basePaths, CancellationToken cancellationToken = default)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary
        };

        int totalFiles = 0;
        long totalBytes = 0;

        await Task.Run(() =>
        {
            foreach (var basePath in basePaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var ignorePatterns = LoadIgnorePatterns(basePath);
                var di = new DirectoryInfo(basePath);
                try
                {
                    foreach (var file in di.EnumerateFiles("*", options))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var rel = Path.GetRelativePath(basePath, file.FullName);
                        if (IsIgnored(rel, ignorePatterns)) continue;

                        totalFiles++;
                        totalBytes += file.Length;
                    }
                }
                catch (UnauthorizedAccessException) { }
            }
        }, cancellationToken);

        return (totalFiles, totalBytes);
    }

    // ────────────────────────────────────────────────
    //  Enumeración de archivos (respeta .backupignore)
    // ────────────────────────────────────────────────

    /// <summary>
    /// Escanea un directorio de forma recursiva y asincrónica respetando .backupignore.
    /// </summary>
    public async IAsyncEnumerable<FileInfo> EnumerateFilesAsync(
        string basePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.Temporary
        };

        var ignorePatterns = LoadIgnorePatterns(basePath);

        var filesList = await Task.Run(
            () => SafeEnumerateFiles(basePath, options, ignorePatterns).ToList(),
            cancellationToken);

        foreach (var file in filesList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return file;
        }
    }

    private IEnumerable<FileInfo> SafeEnumerateFiles(
        string basePath, EnumerationOptions options, HashSet<string> ignorePatterns)
    {
        if (!Directory.Exists(basePath))
            yield break;

        var di = new DirectoryInfo(basePath);
        IEnumerable<FileInfo> files;
        try
        {
            files = di.EnumerateFiles("*", options);
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            var rel = Path.GetRelativePath(basePath, file.FullName);
            if (IsIgnored(rel, ignorePatterns)) continue;
            yield return file;
        }
    }

    // ────────────────────────────────────────────────
    //  Hash  (XXHash64, ultrarrápido)
    // ────────────────────────────────────────────────

    /// <summary>
    /// Calcula el XxHash64 de un archivo. Reintentos si está bloqueado.
    /// Retorna string.Empty si falla definitivamente.
    /// </summary>
    public async Task<string> CalculateXXHash64Async(
        string filePath, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var fileStream = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, useAsync: true);
                var hasher = new XxHash64();
                await hasher.AppendAsync(fileStream, cancellationToken);
                return Convert.ToHexString(hasher.GetCurrentHash());
            }
            catch (IOException) when (attempt < MaxRetries)
            {
                // Archivo bloqueado: esperamos y reintentamos
                await Task.Delay(RetryDelayMs * attempt, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hash] Error irrecuperable en {filePath}: {ex.Message}");
                return string.Empty;
            }
        }
        return string.Empty;
    }

    // ────────────────────────────────────────────────
    //  Copia física segura con reintentos
    // ────────────────────────────────────────────────

    /// <summary>
    /// Intenta copiar un archivo al destino. Si está bloqueado reintentas hasta MaxRetries veces.
    /// Devuelve un FileProcessResult indicando éxito o el motivo del fallo.
    /// </summary>
    public async Task<FileProcessResult> TryCopyWithRetryAsync(
        string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true), cancellationToken);
                return new FileProcessResult { FilePath = sourcePath, Success = true };
            }
            catch (IOException ex) when (attempt < MaxRetries)
            {
                // Archivo bloqueado por otro proceso: esperamos y reintentamos
                await Task.Delay(RetryDelayMs * attempt, cancellationToken);
                Console.WriteLine($"[Copy] Reintento {attempt} para {sourcePath}: {ex.Message}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new FileProcessResult
                {
                    FilePath = sourcePath,
                    Success = false,
                    FailReason = ex.Message
                };
            }
        }

        return new FileProcessResult
        {
            FilePath = sourcePath,
            Success = false,
            FailReason = "Archivo bloqueado después de varios intentos."
        };
    }
}
