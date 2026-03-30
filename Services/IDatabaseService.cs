using LocalBackupMaster.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

public interface IDatabaseService
{
    // ── Sources ──────────────────────────────────────────────────────────────
    Task<BackupSource>         AddSourceAsync(string path);
    Task                       RemoveSourceAsync(int id);
    Task<List<BackupSource>>   GetSourcesAsync();

    // ── Destinations ─────────────────────────────────────────────────────────
    Task<BackupDestination>         AddDestinationAsync(string uuid, string backupPath);
    Task                            RemoveDestinationAsync(int id);
    Task<List<BackupDestination>>   GetDestinationsAsync();
    Task                            UpdateDestinationAsync(BackupDestination destination);

    // ── File Catalog ─────────────────────────────────────────────────────────
    Task<FileRecord?>              GetFileRecordAsync(int destinationId, string relativePath);
    Task                           AddOrUpdateFileRecordAsync(FileRecord record);
    Task<List<FileRecord>>         GetFileRecordsByDestinationAsync(int destinationId);
    Task<FileRecord?>              GetFileRecordByHashAsync(int destinationId, string hash);

    // ── Profiles (C1) ────────────────────────────────────────────────────────
    Task<List<BackupProfile>>  GetProfilesAsync();
    Task<BackupProfile>        SaveProfileAsync(BackupProfile profile);
    Task                       DeleteProfileAsync(int id);

    // ── Report History (D2) ──────────────────────────────────────────────────
    Task                               SaveReportSummaryAsync(BackupReportSummary summary);
    Task<List<BackupReportSummary>>    GetReportHistoryAsync(int limit = 50);
}
