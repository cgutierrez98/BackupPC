using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using System.Text.Json;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

namespace LocalBackupMaster.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private readonly IReportExportService _exportService;
    private readonly INotificationService _notificationService;

    public ReportViewModel(BackupReport report, IReportExportService exportService, INotificationService notificationService)
    {
        Report = report;
        _exportService = exportService;
        _notificationService = notificationService;
    }

    [ObservableProperty]
    private BackupReport _report;

    [RelayCommand]
    public async Task ExportAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                await _notificationService.ShowErrorAsync("Ruta de archivo no válida.");
                return;
            }

            await _exportService.ExportToJsonAsync(Report, filePath);
            await _notificationService.ShowSuccessAsync("Informe exportado correctamente.");
        }
        catch (System.Exception ex)
        {
            await _notificationService.ShowErrorAsync($"Error al exportar: {ex.Message}");
        }
    }

    public Task<string> GetJsonAsync()
    {
        var json = JsonSerializer.Serialize(Report, Options);
        return Task.FromResult(json);
    }
}
