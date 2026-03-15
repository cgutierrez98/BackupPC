using LocalBackupMaster.Models;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

public interface IReportExportService
{
    Task<string> ExportToJsonAsync(BackupReport report, string filePath);
}
