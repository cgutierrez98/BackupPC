using System.Threading.Channels;
using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;
using LocalBackupMaster.Services.Strategies;

namespace LocalBackupMaster.Services.BackupEngine;

public class ParallelBackupEngine : IBackupEngine
{
    private readonly BackupScannerService _scannerService;
    private readonly DatabaseService _databaseService;

    public ParallelBackupEngine(BackupScannerService scannerService, DatabaseService databaseService)
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
        CancellationToken token)
    {
        var startedAt = DateTime.Now;
        var failedFiles = new List<string>();
        var copiedFileNames = new List<string>();
        
        int scannedCount = 0;
        int copiedCount = 0;
        long processedBytes = 0;
        long copiedBytes = 0;

        // 1. Fase de Pre-escaneo
        progress.Report(new BackupProgressReport(BackupPhase.Preparing, "Analizando tamaño total..."));
        var (totalFiles, totalBytes) = await _scannerService.GetTotalFilesAndSizeAsync(sources.Select(s => s.Path), token);

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

                        string relPath = Path.GetRelativePath(source.Path, fi.FullName);
                        string dstPath = Path.Combine(destination.BackupPath!, relPath);

                        // Aplicar Estrategia
                        bool needsCopy = await strategy.ShouldCopyAsync(fi, relPath, destination, token);

                        // Actualizar contadores de escaneo
                        Interlocked.Increment(ref scannedCount);
                        Interlocked.Add(ref processedBytes, fi.Length);

                        progress.Report(new BackupProgressReport(
                            BackupPhase.Scanning,
                            fi.Name,
                            totalBytes > 0 ? (double)processedBytes / totalBytes : 0,
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

                        progress.Report(new BackupProgressReport(
                            BackupPhase.Copying,
                            fi.Name,
                            totalBytes > 0 ? (double)processedBytes / totalBytes : 0,
                            scannedCount,
                            copiedCount,
                            failedFiles.Count,
                            totalBytes,
                            processedBytes));
                    }
                    else
                    {
                        lock (failedFiles) failedFiles.Add($"{fi.Name}: {copyResult.FailReason}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lock (failedFiles) failedFiles.Add($"{fi.Name}: {ex.Message}");
                }
            }
        }, token)).ToArray();

        await producerTask;
        await Task.WhenAll(consumerTasks);

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
}
