using LocalBackupMaster.Models;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

public class JsonReportExportService : IReportExportService
{
    public async Task<string> ExportToJsonAsync(BackupReport report, string filePath)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.Options);
        await File.WriteAllTextAsync(filePath, json);
        return filePath;
    }
}
