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

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly DeviceWatcherService _deviceWatcherService;
    private readonly IBackupEngine _backupEngine;
    private readonly IBackupStrategy _backupStrategy;
    private readonly INavigationService _navigationService;
    private readonly IBackupValidator _backupValidator;

    public ObservableCollection<BackupSource> SourceItems { get; } = [];
    public ObservableCollection<BackupDestination> DestinationItems { get; } = [];

    [ObservableProperty]
    private int _parallelDegree = 4;

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

    public MainViewModel(DatabaseService databaseService,
                         DeviceWatcherService deviceWatcherService,
                         IBackupEngine backupEngine,
                         IBackupStrategy backupStrategy,
                         INavigationService navigationService,
                         IBackupValidator backupValidator)
    {
        _databaseService = databaseService;
        _deviceWatcherService = deviceWatcherService;
        _backupEngine = backupEngine;
        _backupStrategy = backupStrategy;
        _navigationService = navigationService;
        _backupValidator = backupValidator;

        _deviceWatcherService.DeviceConnected += OnDeviceConnected;
        _deviceWatcherService.DeviceDisconnected += OnDeviceDisconnected;
        _deviceWatcherService.StartWatching();
    }

    public async Task InitializeAsync()
    {
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
        ResetStats();

        _backupCts = new CancellationTokenSource();
        var progressReporter = new Progress<BackupProgressReport>(UpdateProgress);

        try
        {
            var report = await _backupEngine.ExecuteAsync(
                SourceItems.ToList(), 
                DestinationItems.First(), 
                _backupStrategy, 
                ParallelDegree, 
                progressReporter, 
                _backupCts.Token);

            await _navigationService.NavigateToAsync(new ReportPage(report));
        }
        catch (OperationCanceledException)
        {
            CurrentFileStatus = "Backup cancelado por el usuario.";
            await _navigationService.DisplayAlertAsync("Cancelado", "La operación fue detenida.", "OK");
        }
        catch (Exception ex)
        {
            await _navigationService.DisplayAlertAsync("Error", $"No pudimos completar el backup:\n{ex.Message}", "OK");
            CurrentFileStatus = "Error durante el backup.";
        }
        finally
        {
            IsBusy = false;
            CanCancel = false;
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
    }

    private void UpdateProgress(BackupProgressReport report)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
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
        });
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };
}
