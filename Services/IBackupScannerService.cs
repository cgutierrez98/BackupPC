using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

public interface IBackupScannerService
{
    Task<(int TotalFiles, long TotalBytes)> GetTotalFilesAndSizeAsync(IEnumerable<string> basePaths, CancellationToken cancellationToken = default);
    IAsyncEnumerable<FileInfo> EnumerateFilesAsync(string basePath, CancellationToken cancellationToken = default);
    Task<string> CalculateXXHash64Async(string filePath, CancellationToken cancellationToken = default);
    Task<FileProcessResult> TryCopyWithRetryAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default);
}
