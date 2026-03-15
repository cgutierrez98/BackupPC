using System;
using System.IO;
using System.IO.Hashing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace LocalBackupMaster.Services;

/// <summary>
/// Resultado del intento de copia de un archivo. Indica si tuvo éxito o fue omitido.
/// </summary>
public class FileProcessResult
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? FailReason { get; set; }
    public bool IsLocked { get; set; }
}

public class BackupScannerService : IBackupScannerService
{
    private const string IgnoreFileName = ".backupignore";
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;

    private readonly AsyncRetryPolicy _retryPolicy;

    public BackupScannerService()
    {
        _retryPolicy = Policy
            .Handle<IOException>()
            .WaitAndRetryAsync(MaxRetries, attempt => TimeSpan.FromMilliseconds(RetryDelayMs * attempt),
                (ex, time, attempt, context) =>
                {
                    Console.WriteLine($"[Resilience] Intento {attempt} tras fallo: {ex.Message}");
                });
    }

    private static readonly HashSet<string> DefaultBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", ".vs", ".DS_Store", "desktop.ini", "Thumbs.db", "$RECYCLE.BIN"
    };

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
    internal static bool IsIgnored(string relativePath, HashSet<string> patterns)
    {
        var segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        // 1. Check against Default Blacklist
        if (segments.Any(s => DefaultBlacklist.Contains(s)))
            return true;

        // 2. Check against .backupignore patterns
        foreach (var pattern in patterns)
        {
            // Comprobación de directorio: si el patrón no tiene *, compara segmentos
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
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                using var fileStream = new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, useAsync: true);
                var hasher = new XxHash64();
                await hasher.AppendAsync(fileStream, cancellationToken);
                return Convert.ToHexString(hasher.GetCurrentHash());
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hash] Error irrecuperable en {filePath}: {ex.Message}");
                return string.Empty;
            }
        });
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
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true), cancellationToken);
                return new FileProcessResult { FilePath = sourcePath, Success = true };
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            bool isLocked = IsFileLocked(ex);
            return new FileProcessResult
            {
                FilePath = sourcePath,
                Success = false,
                FailReason = isLocked ? "El archivo está abierto por otro programa" : ex.Message,
                IsLocked = isLocked
            };
        }
    }

    private static bool IsFileLocked(Exception ex)
    {
        // HResult 0x80070020 = ERROR_SHARING_VIOLATION
        // HResult 0x80070021 = ERROR_LOCK_VIOLATION
        return (uint)ex.HResult == 0x80070020 || (uint)ex.HResult == 0x80070021;
    }
}
