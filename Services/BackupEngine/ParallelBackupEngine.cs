using System.Threading.Channels;
using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;
using LocalBackupMaster.Services.Strategies;

namespace LocalBackupMaster.Services.BackupEngine;

public class ParallelBackupEngine : IBackupEngine
{
    private readonly IBackupScannerService _scannerService;
    private readonly IDatabaseService _databaseService;

    public ParallelBackupEngine(IBackupScannerService scannerService, IDatabaseService databaseService)
    {
        _scannerService = scannerService;
        _databaseService = databaseService;
    }

    public async Task<BackupReport> ExecuteAsync(
        IEnumerable<BackupSource> sources,
        BackupDestination destination,
        IBackupStrategy strategy,
        int parallelDegree,
        IProgress<BackupProgressReport> progress,
        CancellationToken token,
        IEnumerable<string>? includeExtensions = null)
    {
        var startedAt = DateTime.Now;
        var failedFiles = new List<string>();
        var copiedFileNames = new List<string>();
        
        int scannedCount = 0;
        int copiedCount = 0;
        long processedBytes = 0;
        long copiedBytes = 0;

        // Normalizar extensiones (asegurar que empiecen por punto y sean minúsculas)
        var allowedExtensions = includeExtensions?
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLower())
            .Select(e => e.StartsWith(".") ? e : "." + e)
            .ToHashSet();

        bool hasFilter = allowedExtensions != null && allowedExtensions.Count > 0;

        if (string.IsNullOrEmpty(destination.BackupPath) || !Directory.Exists(destination.BackupPath))
        {
            throw new DirectoryNotFoundException($"El destino '{destination.BackupPath}' no está disponible.");
        }

        // 1. Fase de Pre-escaneo
        progress.Report(new BackupProgressReport(BackupPhase.Preparing, "Analizando tamaño total..."));
        
        // El pre-escaneo también debería filtrar para dar un estimado real
        var (totalFiles, totalBytes) = await GetFilteredTotalAsync(sources.Select(s => s.Path), allowedExtensions, token);

        // 2. Setup de Canal
        var channel = Channel.CreateBounded<(FileInfo fi, string relPath, string dstPath)>(
            new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait });

        // 3. PRODUCTOR: Escanea y decide qué copiar usando la Estrategia
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

                        string relPath = Path.GetRelativePath(source.Path, fi.FullName);
                        string dstPath = Path.Combine(destination.BackupPath!, relPath);

                        // Aplicar Estrategia
                        bool needsCopy = await strategy.ShouldCopyAsync(fi, relPath, destination, token);

                        // Actualizar contadores de escaneo
                        Interlocked.Increment(ref scannedCount);
                        Interlocked.Add(ref processedBytes, fi.Length);

                        var currentProgress = totalBytes > 0 ? (double)processedBytes / totalBytes : 0;
                        currentProgress = Math.Clamp(currentProgress, 0, 1);

                        progress.Report(new BackupProgressReport(
                            BackupPhase.Scanning,
                            fi.Name,
                            currentProgress,
                            scannedCount,
                            copiedCount,
                            failedFiles.Count,
                            totalBytes,
                            processedBytes));

                        if (needsCopy)
                        {
                            await channel.Writer.WriteAsync((fi, relPath, dstPath), token);
                        }
                    }
                }
            }
            finally { channel.Writer.TryComplete(); }
        }, token);

        // 4. CONSUMIDORES: Realizan la copia física y actualizan catálogo
        var consumerTasks = Enumerable.Range(0, parallelDegree).Select(i => Task.Run(async () =>
        {
            await foreach (var (fi, relPath, dstPath) in channel.Reader.ReadAllAsync(token))
            {
                try
                {
                    string? dir = Path.GetDirectoryName(dstPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    var copyResult = await _scannerService.TryCopyWithRetryAsync(fi.FullName, dstPath, token);

                    if (copyResult.Success)
                    {
                        // IMPORTANTE: Recalcular hash para persistir en catálogo
                        string hash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
                        await _databaseService.AddOrUpdateFileRecordAsync(new FileRecord
                        {
                            RelativePath = relPath,
                            LastWriteTime = fi.LastWriteTimeUtc,
                            FileSize = fi.Length,
                            FileHash = hash,
                            BackupDestinationId = destination.Id
                        });

                        Interlocked.Add(ref copiedBytes, fi.Length);
                        Interlocked.Increment(ref copiedCount);
                        lock (copiedFileNames) copiedFileNames.Add(fi.Name);

                        var currentProgress = totalBytes > 0 ? (double)processedBytes / totalBytes : 0;
                        currentProgress = Math.Clamp(currentProgress, 0, 1);

                        progress.Report(new BackupProgressReport(
                            BackupPhase.Copying,
                            fi.Name,
                            currentProgress,
                            scannedCount,
                            copiedCount,
                            failedFiles.Count,
                            totalBytes,
                            processedBytes));
                    }
                    else
                    {
                        var failTag = copyResult.IsLocked ? "[BLOQUEADO] " : "[ERROR] ";
                        var failMsg = $"{failTag}{fi.Name}: {copyResult.FailReason}";
                        lock (failedFiles) failedFiles.Add(failMsg);

                        var currentProgress = totalBytes > 0 ? (double)processedBytes / totalBytes : 0;
                        currentProgress = Math.Clamp(currentProgress, 0, 1);

                        progress.Report(new BackupProgressReport(
                            BackupPhase.Copying,
                            fi.Name,
                            currentProgress,
                            scannedCount,
                            copiedCount,
                            failedFiles.Count,
                            totalBytes,
                            processedBytes,
                            LastErrorMessage: failMsg,
                            LastErrorIsWarning: copyResult.IsLocked));
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    var errorMsg = $"[CRÍTICO] {fi.Name}: {ex.Message}";
                    lock (failedFiles) failedFiles.Add(errorMsg);
                    
                    progress.Report(new BackupProgressReport(
                        BackupPhase.Copying,
                        fi.Name,
                        0,
                        scannedCount,
                        copiedCount,
                        failedFiles.Count,
                        totalBytes,
                        processedBytes,
                        LastErrorMessage: errorMsg));
                }
            }
        }, token)).ToArray();

        await producerTask;
        await Task.WhenAll(consumerTasks);

        progress.Report(new BackupProgressReport(BackupPhase.Completed, "Generando reporte final..."));

        return new BackupReport
        {
            StartedAt = startedAt,
            FinishedAt = DateTime.Now,
            TotalScanned = scannedCount,
            TotalCopied = copiedCount,
            TotalSkipped = Math.Max(0, scannedCount - copiedCount - failedFiles.Count),
            TotalFailed = failedFiles.Count,
            TotalBytesCopied = copiedBytes,
            ParallelDegree = parallelDegree,
            FailedFiles = failedFiles,
            CopiedFiles = copiedFileNames
        };
    }

    private async Task<(int TotalFiles, long TotalBytes)> GetFilteredTotalAsync(
        IEnumerable<string> paths, HashSet<string>? allowedExts, CancellationToken token)
    {
        // Si no hay filtro, usar el método rápido del servicio
        if (allowedExts == null || allowedExts.Count == 0)
            return await _scannerService.GetTotalFilesAndSizeAsync(paths, token);

        int totalFiles = 0;
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
