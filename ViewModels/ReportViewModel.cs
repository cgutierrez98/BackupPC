using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using System.Text.Json;
using System.Collections.ObjectModel;

namespace LocalBackupMaster.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IReportExportService _exportService;
    private readonly INotificationService _notificationService;

    public ReportViewModel(BackupReport report, IReportExportService exportService, INotificationService notificationService)
    {
        _exportService = exportService;
        _notificationService = notificationService;

        // Inicializar propiedades desde el modelo
        Report = report;
        ResultEmoji = report.ResultEmoji;
        ResultText = report.ResultText;
        DurationText = report.DurationText;
        DateText = report.FinishedAt.ToString("dddd, d MMMM yyyy — HH:mm");
        ChipScanned = report.TotalScanned.ToString("N0");
        ChipCopied = report.TotalCopied.ToString("N0");
        ChipSize = report.SizeText;
        ChipFailed = report.TotalFailed.ToString("N0");
        FailedFiles = new ObservableCollection<string>(report.FailedFiles ?? Enumerable.Empty<string>());
        CopiedFiles = new ObservableCollection<string>(report.CopiedFiles ?? Enumerable.Empty<string>());
        FailedCardVisible = report.TotalFailed > 0;
        CopiedCardVisible = report.CopiedFiles != null && report.CopiedFiles.Count > 0;
        CopiedCountText = report.CopiedFiles != null
            ? (report.CopiedFiles.Count > 200 ? $"(mostrando 200 de {report.CopiedFiles.Count})" : $"{report.CopiedFiles.Count} archivo/s")
            : string.Empty;
    }

    [ObservableProperty]
    private BackupReport _report;

    [ObservableProperty]
    private string _resultEmoji;

    [ObservableProperty]
    private string _resultText;

    [ObservableProperty]
    private string _durationText;

    [ObservableProperty]
    private string _dateText;

    [ObservableProperty]
    private string _chipScanned;

    [ObservableProperty]
    private string _chipCopied;

    [ObservableProperty]
    private string _chipSize;

    [ObservableProperty]
    private string _chipFailed;

    [ObservableProperty]
    private ObservableCollection<string> _failedFiles = new();

    [ObservableProperty]
    private ObservableCollection<string> _copiedFiles = new();

    [ObservableProperty]
    private bool _failedCardVisible;

    [ObservableProperty]
    private bool _copiedCardVisible;

    [ObservableProperty]
    private string _copiedCountText;

    [RelayCommand]
    public async Task ExportAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                await _notification_service.ShowErrorAsync("Ruta de archivo no válida.");
                return;
            }

            await _exportService.ExportToJsonAsync(Report, filePath);
            await _notification_service.ShowSuccessAsync("Informe exportado correctamente.");
        }
        catch (System.Exception ex)
        {
            await _notification_service.ShowErrorAsync($"Error al exportar: {ex.Message}");
        }
    }

    public Task<string> GetJsonAsync()
    {
        var json = JsonSerializer.Serialize(Report, Options);
        return Task.FromResult(json);
    }
}
