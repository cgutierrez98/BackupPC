using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalBackupMaster.Models;
using LocalBackupMaster.Models.Backup;
using LocalBackupMaster.Services;
using LocalBackupMaster.Services.BackupEngine;
using LocalBackupMaster.Services.Strategies;
using LocalBackupMaster.Services.Navigation;
using LocalBackupMaster.Services.Validation;
using CommunityToolkit.Maui.Storage;

namespace LocalBackupMaster.ViewModels;

public record LogEntry(string Message, string Timestamp, Color Color);

public partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly DeviceWatcherService _deviceWatcherService;
    private readonly IBackupEngine _backupEngine;
    private readonly IBackupStrategy _backupStrategy;
    private readonly INavigationService _navigationService;
    private readonly IBackupValidator _backupValidator;
    private readonly INotificationService _notificationService;

    private readonly Dictionary<string, string> _filterGroups = new()
    {
        { "photos", ".jpg, .jpeg, .png, .heic, .raw" },
        { "docs", ".pdf, .docx, .xlsx, .pptx, .txt" },
        { "video", ".mp4, .mov, .mkv, .avi" },
        { "audio", ".mp3, .wav, .flac, .aac" }
    };

    public ObservableCollection<BackupSource> SourceItems { get; } = [];
    public ObservableCollection<BackupDestination> DestinationItems { get; } = [];
    public ObservableCollection<LogEntry> LogItems { get; } = [];

    [ObservableProperty]
    private int _parallelDegree = 4;

    [ObservableProperty]
    private string _includeExtensions = "";

    // Propiedades para estado visual de los botones
    [ObservableProperty] private bool _isPhotosActive;
    [ObservableProperty] private bool _isDocsActive;
    [ObservableProperty] private bool _isVideoActive;
    [ObservableProperty] private bool _isAudioActive;
    [ObservableProperty] private bool _isAllActive = true;
    [ObservableProperty] private bool _showActivityLog;

    [ObservableProperty]
    private string _currentFileStatus = "Listo para comenzar";

    [ObservableProperty]
    private double _globalProgress = 0;

    [ObservableProperty]
    private string _percentageText = "0%";

    [ObservableProperty]
    private int _statsScanned = 0;

    [ObservableProperty]
    private int _statsCopied = 0;

    [ObservableProperty]
    private int _statsFailed = 0;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canCancel;

    private CancellationTokenSource? _backupCts;

    public MainViewModel(IDatabaseService databaseService,
                         DeviceWatcherService deviceWatcherService,
                         IBackupEngine backupEngine,
                         IBackupStrategy backupStrategy,
                         INavigationService navigationService,
                         IBackupValidator backupValidator,
                         INotificationService notificationService)
    {
        _databaseService = databaseService;
        _deviceWatcherService = deviceWatcherService;
        _backupEngine = backupEngine;
        _backupStrategy = backupStrategy;
        _navigationService = navigationService;
        _backupValidator = backupValidator;
        _notificationService = notificationService;

        _deviceWatcherService.DeviceConnected += OnDeviceConnected;
        _deviceWatcherService.DeviceDisconnected += OnDeviceDisconnected;
        _deviceWatcherService.StartWatching();
    }

    public async Task InitializeAsync()
    {
        ShowActivityLog = false;
        SourceItems.Clear();
        foreach (var s in await _databaseService.GetSourcesAsync())
            SourceItems.Add(s);

        DestinationItems.Clear();
        foreach (var d in await _databaseService.GetDestinationsAsync())
            DestinationItems.Add(d);
    }

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            bool answer = await _navigationService.DisplayAlertAsync("Nuevo disco detectado",
                $"¿Deseas registrar '{e.VolumeLabel} ({e.DrivePath})' como unidad de destino?",
                "Sí, registrar", "No");

            if (answer)
            {
                var uuid = e.VolumeLabel + "_" + Guid.NewGuid().ToString()[..4];
                var dest = await _databaseService.AddDestinationAsync(uuid, e.DrivePath);
                DestinationItems.Add(dest);
            }
        });
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
        => Console.WriteLine($"Disco desconectado: {e.DrivePath}");

    [RelayCommand]
    private async Task SelectSource()
    {
        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful) return;

        var folderPath = result.Folder.Path;
        if (SourceItems.Any(s => s.Path == folderPath)) return;

        var source = await _databaseService.AddSourceAsync(folderPath);
        SourceItems.Add(source);
    }

    [RelayCommand]
    private async Task RemoveSource(BackupSource src)
    {
        SourceItems.Remove(src);
        await _databaseService.RemoveSourceAsync(src.Id);
    }

    [RelayCommand]
    private async Task SelectDestination()
    {
        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful) return;

        var folderPath = result.Folder.Path;
        if (DestinationItems.Any(d => d.BackupPath == folderPath)) return;

        var uuid = Guid.NewGuid().ToString();
        var dest = await _databaseService.AddDestinationAsync(uuid, folderPath);
        DestinationItems.Add(dest);
    }

    [RelayCommand]
    private async Task RemoveDestination(BackupDestination dst)
    {
        DestinationItems.Remove(dst);
        await _databaseService.RemoveDestinationAsync(dst.Id);
    }

    [RelayCommand]
    private void ToggleFilter(string category)
    {
        category = category.ToLower();

        if (category == "all")
        {
            ResetFilters();
            return;
        }

        // Toggle el estado
        switch (category)
        {
            case "photos": IsPhotosActive = !IsPhotosActive; break;
            case "docs": IsDocsActive = !IsDocsActive; break;
            case "video": IsVideoActive = !IsVideoActive; break;
            case "audio": IsAudioActive = !IsAudioActive; break;
        }

        UpdateIncludeExtensionsFromActiveFilters();
    }

    private void ResetFilters()
    {
        IsPhotosActive = false;
        IsDocsActive = false;
        IsVideoActive = false;
        IsAudioActive = false;
        IsAllActive = true;
        IncludeExtensions = "";
    }

    private void UpdateIncludeExtensionsFromActiveFilters()
    {
        var activeExtensions = new List<string>();

        if (IsPhotosActive) activeExtensions.Add(_filterGroups["photos"]);
        if (IsDocsActive) activeExtensions.Add(_filterGroups["docs"]);
        if (IsVideoActive) activeExtensions.Add(_filterGroups["video"]);
        if (IsAudioActive) activeExtensions.Add(_filterGroups["audio"]);

        if (activeExtensions.Count == 0)
        {
            IsAllActive = true;
            IncludeExtensions = "";
        }
        else
        {
            IsAllActive = false;
            IncludeExtensions = string.Join(", ", activeExtensions);
        }
    }

    [RelayCommand]
    private void CancelBackup()
    {
        _backupCts?.Cancel();
        CurrentFileStatus = "Cancelando operación...";
        CanCancel = false;
    }

    [RelayCommand]
    private async Task StartBackup()
    {
        var validation = _backupValidator.Validate(SourceItems, DestinationItems);
        if (!validation.IsValid)
        {
            await _navigationService.DisplayAlertAsync("Datos incompletos", validation.Message, "OK");
            return;
        }

        IsBusy = true;
        CanCancel = true;
        ShowActivityLog = true;
        ResetStats();

        _backupCts = new CancellationTokenSource();
        var progressReporter = new Progress<BackupProgressReport>(UpdateProgress);

        var extensions = IncludeExtensions?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            var destination = DestinationItems.FirstOrDefault();
            if (destination == null)
            {
                await _navigationService.DisplayAlertAsync("Atención", "No hay un destino seleccionado.", "OK");
                return;
            }

            var report = await _backupEngine.ExecuteAsync(
                SourceItems.ToList(), 
                destination, 
                _backupStrategy, 
                ParallelDegree, 
                progressReporter, 
                _backupCts.Token,
                extensions);

            if (report == null) throw new Exception("El motor de backup no generó un reporte.");

            // Aseguramos que la UI refleja el 100% antes de saltar
            GlobalProgress = 1.0;
            PercentageText = "100%";
            CurrentFileStatus = "Finalizado. Preparando reporte...";
            
            // Un pequeño respiro para que WinUI termine animaciones de la ProgressBar
            await Task.Delay(500);

            // Navegar en el hilo principal de forma explícita
            await MainThread.InvokeOnMainThreadAsync(async () => {
                try {
                    await _navigationService.NavigateToAsync(new ReportPage(report));
                } catch (Exception navEx) {
                    AddLogEntry($"ERROR NAV: {navEx.Message}", Colors.Red);
                    await _navigationService.DisplayAlertAsync("Error de Navegación", $"No se pudo abrir la pantalla de reporte: {navEx.Message}", "OK");
                }
            });
        }
        catch (OperationCanceledException)
        {
            CurrentFileStatus = "Backup cancelado por el usuario.";
            await _navigationService.DisplayAlertAsync("Cancelado", "La operación fue detenida.", "OK");
        }
        catch (Exception ex)
        {
            // Loguear error técnico completo para depuración
            var inner = ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : "";
            var errorMsg = $"Error: {ex.Message}{inner}\nStack: {ex.StackTrace}";
            
            AddLogEntry($"CRASH: {ex.Message}", Colors.Red);
            if (ex.InnerException != null) AddLogEntry($"INNER: {ex.InnerException.Message}", Colors.Red);
            
            Console.WriteLine(errorMsg);
            
            await _navigationService.DisplayAlertAsync("Error de Ejecución", 
                $"No pudimos completar el backup:\n{ex.Message}{inner}\n\nRevisa el log de actividad para más detalles.", "OK");
            
            CurrentFileStatus = "Error fatal durante la ejecución.";
            ShowActivityLog = true; // Asegurar que el log se queda visible
        }
        finally
        {
            IsBusy = false;
            CanCancel = false;
            // No reseteamos ShowActivityLog aquí para que el usuario pueda leer el error
            _backupCts?.Dispose();
            _backupCts = null;
        }
    }

    private void ResetStats()
    {
        GlobalProgress = 0;
        PercentageText = "0%";
        StatsScanned = 0;
        StatsCopied = 0;
        StatsFailed = 0;
        CurrentFileStatus = "Iniciando...";
        LogItems.Clear();
    }

    private void UpdateProgress(BackupProgressReport report)
    {
        // Si ya no estamos en modo busy (ej: terminando o cancelando), ignorar updates tardíos
        if (!IsBusy && report.Phase != BackupPhase.Completed) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!IsBusy && report.Phase != BackupPhase.Completed) return;

            StatsScanned = report.ScannedCount;
            StatsCopied = report.CopiedCount;
            StatsFailed = report.FailedCount;
            GlobalProgress = report.Progress;
            PercentageText = $"{(int)(report.Progress * 100)}%";
            
            CurrentFileStatus = report.Phase switch
            {
                BackupPhase.Preparing => "Preparando motores...",
                BackupPhase.Scanning => $"Analizando: {report.CurrentItem}",
                BackupPhase.Copying => $"Copiando: {report.CurrentItem}",
                _ => CurrentFileStatus
            };

            if (report.Phase == BackupPhase.Copying && !string.IsNullOrEmpty(report.CurrentItem))
            {
                AddLogEntry($"Copiado: {report.CurrentItem}", 
                    Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.DimGray);
            }
            else if (report.Phase == BackupPhase.Scanning && report.FailedCount > StatsFailed)
            {
                AddLogEntry($"Error/Omitido: {report.CurrentItem}", Colors.Red);
            }
        });
    }

    private void AddLogEntry(string message, Color color)
    {
        var entry = new LogEntry(message, DateTime.Now.ToString("HH:mm:ss"), color);
        if (LogItems.Count >= 100)
            LogItems.RemoveAt(0);
        LogItems.Add(entry);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };
}
