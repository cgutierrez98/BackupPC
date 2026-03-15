using LocalBackupMaster.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

public interface IDatabaseService
{
    Task<BackupSource> AddSourceAsync(string path);
    Task RemoveSourceAsync(int id);
    Task<List<BackupSource>> GetSourcesAsync();
    Task<BackupDestination> AddDestinationAsync(string uuid, string backupPath);
    Task RemoveDestinationAsync(int id);
    Task<List<BackupDestination>> GetDestinationsAsync();
    Task<FileRecord?> GetFileRecordAsync(int destinationId, string relativePath);
    Task AddOrUpdateFileRecordAsync(FileRecord record);
}
