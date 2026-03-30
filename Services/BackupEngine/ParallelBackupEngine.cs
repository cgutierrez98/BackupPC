using System.Diagnostics;
using System.Threading.Channels;
using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;
using LocalBackupMaster.Services.Strategies;

namespace LocalBackupMaster.Services.BackupEngine;

public class ParallelBackupEngine : IBackupEngine
{
    private readonly IBackupScannerService  _scannerService;
    private readonly IDatabaseService       _databaseService;
    private readonly IEncryptionService     _encryption;
    private readonly IVersionCleanupService _versioning;

    public ParallelBackupEngine(
        IBackupScannerService  scannerService,
        IDatabaseService       databaseService,
        IEncryptionService     encryption,
        IVersionCleanupService versioning)
    {
        _scannerService  = scannerService;
        _databaseService = databaseService;
        _encryption      = encryption;
        _versioning      = versioning;
    }

    public async Task<BackupReport> ExecuteAsync(
        IEnumerable<BackupSource>          sources,
        BackupDestination                  destination,
        IBackupStrategy                    strategy,
        int                                parallelDegree,
        IProgress<BackupProgressReport>    progress,
        CancellationToken                  token,
        IEnumerable<string>?               includeExtensions = null,
        bool                               dryRun            = false,
        DateTimeOffset?                    sinceDate         = null)
    {
        var startedAt      = DateTime.Now;
        var failedFiles    = new List<string>();
        var copiedFileNames = new List<string>();

        int  scannedCount    = 0;
        int  copiedCount     = 0;
        long processedBytes  = 0;
        long copiedBytes     = 0;

        // Normalizar extensiones
        var allowedExtensions = includeExtensions?
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLower())
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet();

        bool hasFilter = allowedExtensions?.Count > 0;

        if (string.IsNullOrEmpty(destination.BackupPath) || !Directory.Exists(destination.BackupPath))
            throw new DirectoryNotFoundException($"El destino '{destination.BackupPath}' no está disponible.");

        // 1. Pre-escaneo ────────────────────────────────────────────────────
        progress.Report(new BackupProgressReport(BackupPhase.Preparing,
            dryRun ? "Simulacro — analizando tamaño total..." : "Analizando tamaño total..."));

        var (_, totalBytes) = await GetFilteredTotalAsync(
            sources.Select(s => s.Path), allowedExtensions, token);

        // 2. Canal ──────────────────────────────────────────────────────────
        var channel = Channel.CreateBounded<(FileInfo fi, string relPath, string dstPath)>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait });

        // Throttle: máximo KB/s (0 = sin límite)
        int throttleKBps = destination.ThrottleKBps;

        // 3. PRODUCTOR ──────────────────────────────────────────────────────
        var producerTask = Task.Run(async () =>
        {
            try
            {
                foreach (var source in sources)
                {
                    if (!Directory.Exists(source.Path)) continue;

                    await foreach (var fi in _scannerService.EnumerateFilesAsync(source.Path, token))
                    {
                        token.ThrowIfCancellationRequested();

                        // Filtro de extensiones
                        if (hasFilter && !allowedExtensions!.Contains(fi.Extension.ToLower()))
                            continue;

                        // A3 — Filtro por fecha de modificación
                        if (sinceDate.HasValue && fi.LastWriteTimeUtc < sinceDate.Value.UtcDateTime)
                            continue;

                        string relPath = Path.GetRelativePath(source.Path, fi.FullName);
                        string dstPath = Path.Combine(destination.BackupPath!, relPath);

                        bool needsCopy = await strategy.ShouldCopyAsync(fi, relPath, destination, token);

                        Interlocked.Increment(ref scannedCount);
                        Interlocked.Add(ref processedBytes, fi.Length);

                        var scanPct = totalBytes > 0
                            ? Math.Clamp((double)processedBytes / totalBytes * 0.5, 0, 0.5)
                            : 0;

                        progress.Report(new BackupProgressReport(
                            BackupPhase.Scanning, fi.Name, scanPct,
                            scannedCount, copiedCount, failedFiles.Count,
                            totalBytes, processedBytes));

                        if (needsCopy)
                            await channel.Writer.WriteAsync((fi, relPath, dstPath), token);
                    }
                }
            }
            finally { channel.Writer.TryComplete(); }
        }, token);

        // 4. CONSUMIDORES ───────────────────────────────────────────────────
        var consumerTasks = Enumerable.Range(0, parallelDegree).Select(_ => Task.Run(async () =>
        {
            await foreach (var (fi, relPath, dstPath) in channel.Reader.ReadAllAsync(token))
            {
                try
                {
                    // C3 — Deduplicación por hash antes de copiar
                    if (!dryRun && destination.VersioningEnabled is false)
                    {
                        var srcHash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
                        var dup     = await _databaseService.GetFileRecordByHashAsync(destination.Id, srcHash);
                        if (dup != null && dup.RelativePath != relPath)
                        {
                            // Mismo contenido ya en catálogo → solo actualizar registro
                            await _databaseService.AddOrUpdateFileRecordAsync(new FileRecord
                            {
                                RelativePath       = relPath,
                                LastWriteTime      = fi.LastWriteTimeUtc,
                                FileSize           = fi.Length,
                                FileHash           = srcHash,
                                BackupDestinationId = destination.Id,
                                IsEncrypted        = destination.IsEncrypted
                            });
                            Interlocked.Increment(ref copiedCount);
                            lock (copiedFileNames) copiedFileNames.Add(fi.Name + " [dedup]");
                            continue;
                        }
                    }

                    // C2 — Versionado: archivar versión anterior antes de sobrescribir
                    if (!dryRun && destination.VersioningEnabled && File.Exists(dstPath))
                    {
                        var verDir      = _versioning.GetVersionsDir(destination.BackupPath!, relPath);
                        Directory.CreateDirectory(verDir);
                        var verFileName = _versioning.CreateVersionFileName(Path.GetFileName(dstPath));
                        File.Move(dstPath, Path.Combine(verDir, verFileName));

                        // Limpiar versiones antiguas
                        await _versioning.CleanupAsync(destination.BackupPath!, relPath, destination.MaxVersions, token);

                        // Actualizar contador de versiones en FileRecord
                        var existing = await _databaseService.GetFileRecordAsync(destination.Id, relPath);
                        if (existing != null)
                        {
                            existing.VersionCount++;
                            await _databaseService.AddOrUpdateFileRecordAsync(existing);
                        }
                    }

                    if (!dryRun)
                    {
                        string? dir = Path.GetDirectoryName(dstPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                        string hash;

                        // B1 — Cifrado
                        if (destination.IsEncrypted)
                        {
                            await _encryption.EncryptFileAsync(fi.FullName, dstPath, destination.Uuid, token);
                            hash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
                        }
                        else
                        {
                            var copyResult = await _scannerService.TryCopyWithRetryAsync(fi.FullName, dstPath, token);

                            if (!copyResult.Success)
                            {
                                var failTag = copyResult.IsLocked ? "[BLOQUEADO] " : "[ERROR] ";
                                var failMsg = $"{failTag}{fi.Name}: {copyResult.FailReason}";
                                lock (failedFiles) failedFiles.Add(failMsg);

                                var errPct = Math.Clamp(0.5 + (totalBytes > 0 ? (double)copiedBytes / totalBytes * 0.5 : 0), 0.5, 1.0);
                                progress.Report(new BackupProgressReport(
                                    BackupPhase.Copying, fi.Name, errPct,
                                    scannedCount, copiedCount, failedFiles.Count,
                                    totalBytes, processedBytes,
                                    LastErrorMessage: failMsg, LastErrorIsWarning: copyResult.IsLocked));
                                continue;
                            }

                            hash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
                        }

                        // Persistir en catálogo
                        await _databaseService.AddOrUpdateFileRecordAsync(new FileRecord
                        {
                            RelativePath        = relPath,
                            LastWriteTime       = fi.LastWriteTimeUtc,
                            FileSize            = fi.Length,
                            FileHash            = hash,
                            BackupDestinationId = destination.Id,
                            IsEncrypted         = destination.IsEncrypted
                        });

                        Interlocked.Add(ref copiedBytes, fi.Length);

                        // A2 — Throttle de ancho de banda
                        if (throttleKBps > 0)
                        {
                            long maxBytesPerSec = throttleKBps * 1024L;
                            var  requiredMs = (int)(fi.Length * 1000L / maxBytesPerSec);
                            if (requiredMs > 0)
                                await Task.Delay(requiredMs, token);
                        }
                    }

                    Interlocked.Increment(ref copiedCount);
                    lock (copiedFileNames) copiedFileNames.Add(fi.Name);

                    var copyPct = Math.Clamp(0.5 + (totalBytes > 0 ? (double)copiedBytes / totalBytes * 0.5 : 0), 0.5, 1.0);
                    progress.Report(new BackupProgressReport(
                        BackupPhase.Copying, fi.Name, copyPct,
                        scannedCount, copiedCount, failedFiles.Count,
                        totalBytes, processedBytes));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    var errorMsg = $"[CRÍTICO] {fi.Name}: {ex.Message}";
                    lock (failedFiles) failedFiles.Add(errorMsg);

                    progress.Report(new BackupProgressReport(
                        BackupPhase.Copying, fi.Name, 0,
                        scannedCount, copiedCount, failedFiles.Count,
                        totalBytes, processedBytes,
                        LastErrorMessage: errorMsg));
                }
            }
        }, token)).ToArray();

        await producerTask;
        await Task.WhenAll(consumerTasks);

        progress.Report(new BackupProgressReport(BackupPhase.Completed, "Generando reporte final..."));

        return new BackupReport
        {
            StartedAt        = startedAt,
            FinishedAt       = DateTime.Now,
            TotalScanned     = scannedCount,
            TotalCopied      = copiedCount,
            TotalSkipped     = Math.Max(0, scannedCount - copiedCount - failedFiles.Count),
            TotalFailed      = failedFiles.Count,
            TotalBytesCopied = copiedBytes,
            ParallelDegree   = parallelDegree,
            WasDryRun        = dryRun,
            FailedFiles      = failedFiles,
            CopiedFiles      = copiedFileNames
        };
    }

    private async Task<(int TotalFiles, long TotalBytes)> GetFilteredTotalAsync(
        IEnumerable<string> paths, HashSet<string>? allowedExts, CancellationToken token)
    {
        if (allowedExts == null || allowedExts.Count == 0)
            return await _scannerService.GetTotalFilesAndSizeAsync(paths, token);

        int  totalFiles = 0;
        long totalBytes = 0;

        foreach (var path in paths)
        {
            if (!Directory.Exists(path)) continue;

            await foreach (var fi in _scannerService.EnumerateFilesAsync(path, token))
            {
                token.ThrowIfCancellationRequested();
                if (allowedExts.Contains(fi.Extension.ToLower()))
                {
                    totalFiles++;
                    totalBytes += fi.Length;
                }
            }
        }

        return (totalFiles, totalBytes);
    }
}
