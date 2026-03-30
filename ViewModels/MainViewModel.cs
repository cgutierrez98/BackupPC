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
using Microsoft.Extensions.DependencyInjection;

namespace LocalBackupMaster.ViewModels;

public record LogEntry(string Message, string Timestamp, Color Color);

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IDatabaseService        _databaseService;
    private readonly DeviceWatcherService    _deviceWatcherService;
    private readonly IBackupEngine           _backupEngine;
    private readonly IBackupStrategy         _backupStrategy;
    private readonly INavigationService      _navigationService;
    private readonly IBackupValidator        _backupValidator;
    private readonly INotificationService    _notificationService;
    private readonly IServiceProvider        _serviceProvider;

    private readonly Dictionary<string, string> _filterGroups = new()
    {
        { "photos", ".jpg, .jpeg, .png, .heic, .raw" },
        { "docs",   ".pdf, .docx, .xlsx, .pptx, .txt" },
        { "video",  ".mp4, .mov, .mkv, .avi" },
        { "audio",  ".mp3, .wav, .flac, .aac" }
    };

    // ── Colecciones ────────────────────────────────────────────────────────
    public ObservableCollection<BackupSource>      SourceItems      { get; } = [];
    public ObservableCollection<BackupDestination> DestinationItems { get; } = [];
    public ObservableCollection<LogEntry>          LogItems         { get; } = [];
    public ObservableCollection<BackupProfile>     Profiles         { get; } = [];

    // ── Opciones de ejecución ──────────────────────────────────────────────
    [ObservableProperty] private int  _parallelDegree   = 4;
    [ObservableProperty] private string _includeExtensions = "";

    // A1 — Dry-Run
    [ObservableProperty] private bool _isDryRun;

    // A3 — Filtro por fecha
    [ObservableProperty] private bool     _useSinceDate;
    [ObservableProperty] private DateTime _sinceDate = DateTime.Today.AddDays(-30);

    // C1 — Perfiles
    [ObservableProperty] private BackupProfile? _selectedProfile;
    [ObservableProperty] private string _newProfileName = "";

    // ── Estado visual de filtros ───────────────────────────────────────────
    [ObservableProperty] private bool _isPhotosActive;
    [ObservableProperty] private bool _isDocsActive;
    [ObservableProperty] private bool _isVideoActive;
    [ObservableProperty] private bool _isAudioActive;
    [ObservableProperty] private bool _isAllActive = true;
    [ObservableProperty] private bool _showActivityLog;

    // ── Estado de ejecución ────────────────────────────────────────────────
    [ObservableProperty] private string _currentFileStatus  = "Listo para comenzar";
    [ObservableProperty] private double _globalProgress     = 0;
    [ObservableProperty] private string _percentageText     = "0%";
    [ObservableProperty] private int    _statsScanned       = 0;
    [ObservableProperty] private int    _statsCopied        = 0;
    [ObservableProperty] private int    _statsFailed        = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartBackup))]
    private bool _isBusy;

    public bool CanStartBackup => !IsBusy;

    [ObservableProperty] private bool _canCancel;

    // C4 — Undo temporal
    private BackupSource?      _pendingDeleteSource;
    private BackupDestination? _pendingDeleteDestination;

    private CancellationTokenSource? _backupCts;

    public MainViewModel(IDatabaseService      databaseService,
                         DeviceWatcherService  deviceWatcherService,
                         IBackupEngine         backupEngine,
                         IBackupStrategy       backupStrategy,
                         INavigationService    navigationService,
                         IBackupValidator      backupValidator,
                         INotificationService  notificationService,
                         IServiceProvider      serviceProvider)
    {
        _databaseService      = databaseService;
        _deviceWatcherService = deviceWatcherService;
        _backupEngine         = backupEngine;
        _backupStrategy       = backupStrategy;
        _navigationService    = navigationService;
        _backupValidator      = backupValidator;
        _notificationService  = notificationService;
        _serviceProvider      = serviceProvider;

        _deviceWatcherService.DeviceConnected    += OnDeviceConnected;
        _deviceWatcherService.DeviceDisconnected += OnDeviceDisconnected;
        _deviceWatcherService.StartWatching();
    }

    public void Dispose()
    {
        _deviceWatcherService.DeviceConnected    -= OnDeviceConnected;
        _deviceWatcherService.DeviceDisconnected -= OnDeviceDisconnected;
        _deviceWatcherService.StopWatching();
        _backupCts?.Dispose();
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

        Profiles.Clear();
        foreach (var p in await _databaseService.GetProfilesAsync())
            Profiles.Add(p);
    }

    // ── Device watcher ─────────────────────────────────────────────────────

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // D1 — Auto-backup si algún destino tiene AutoBackupOnConnect
            var autoTarget = DestinationItems.FirstOrDefault(d =>
                d.AutoBackupOnConnect && d.BackupPath?.StartsWith(e.DrivePath) == true);

            if (autoTarget != null && !IsBusy)
            {
                AddLogEntry($"Auto-backup iniciado para '{e.VolumeLabel}'", Colors.CornflowerBlue);
                await StartBackupAsync(autoTarget);
                return;
            }

            bool answer = await _navigationService.DisplayAlertAsync("Nuevo disco detectado",
                $"¿Registrar '{e.VolumeLabel} ({e.DrivePath})' como destino?",
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

    // ── Source / Destination commands ──────────────────────────────────────

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
        // C4 — guardar para posible undo (ventana de 5 s)
        _pendingDeleteSource = src;
        SourceItems.Remove(src);
        await _databaseService.RemoveSourceAsync(src.Id);
        AddLogEntry($"Fuente eliminada: {src.Path}", Colors.Orange);
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
        _pendingDeleteDestination = dst;
        DestinationItems.Remove(dst);
        await _databaseService.RemoveDestinationAsync(dst.Id);
        AddLogEntry($"Destino eliminado: {dst.BackupPath}", Colors.Orange);
    }

    // ── Filter commands ────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleFilter(string category)
    {
        category = category.ToLower();

        if (category == "all") { ResetFilters(); return; }

        switch (category)
        {
            case "photos": IsPhotosActive = !IsPhotosActive; break;
            case "docs":   IsDocsActive   = !IsDocsActive;   break;
            case "video":  IsVideoActive  = !IsVideoActive;  break;
            case "audio":  IsAudioActive  = !IsAudioActive;  break;
        }

        UpdateIncludeExtensionsFromActiveFilters();
    }

    private void ResetFilters()
    {
        IsPhotosActive = false;
        IsDocsActive   = false;
        IsVideoActive  = false;
        IsAudioActive  = false;
        IsAllActive    = true;
        IncludeExtensions = "";
    }

    private void UpdateIncludeExtensionsFromActiveFilters()
    {
        var active = new List<string>();
        if (IsPhotosActive) active.Add(_filterGroups["photos"]);
        if (IsDocsActive)   active.Add(_filterGroups["docs"]);
        if (IsVideoActive)  active.Add(_filterGroups["video"]);
        if (IsAudioActive)  active.Add(_filterGroups["audio"]);

        if (active.Count == 0) { IsAllActive = true; IncludeExtensions = ""; }
        else                   { IsAllActive = false; IncludeExtensions = string.Join(", ", active); }
    }

    // ── C1: Profile commands ───────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveProfile()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await _navigationService.DisplayAlertAsync("Perfil", "Ingresa un nombre para el perfil.", "OK");
            return;
        }

        var profile = SelectedProfile ?? new BackupProfile { Name = name };
        profile.Name               = name;
        profile.IncludeExtensions  = IncludeExtensions;
        profile.ParallelDegree     = ParallelDegree;
        profile.DryRunByDefault    = IsDryRun;
        profile.SetSourceIds(SourceItems.Select(s => s.Id));
        profile.SetDestinationIds(DestinationItems.Select(d => d.Id));

        var saved = await _databaseService.SaveProfileAsync(profile);

        if (SelectedProfile == null)
            Profiles.Add(saved);
        else
        {
            var idx = Profiles.IndexOf(SelectedProfile);
            if (idx >= 0) Profiles[idx] = saved;
        }

        SelectedProfile = saved;
        AddLogEntry($"Perfil '{saved.Name}' guardado.", Colors.MediumSeaGreen);
    }

    [RelayCommand]
    private async Task LoadProfile(BackupProfile profile)
    {
        SelectedProfile   = profile;
        NewProfileName    = profile.Name;
        IncludeExtensions = profile.IncludeExtensions ?? "";
        ParallelDegree    = profile.ParallelDegree;
        IsDryRun          = profile.DryRunByDefault;

        // Recargar fuentes y destinos según perfil
        var allSources      = await _databaseService.GetSourcesAsync();
        var allDests        = await _databaseService.GetDestinationsAsync();
        var profileSourceIds = profile.GetSourceIds().ToHashSet();
        var profileDestIds   = profile.GetDestinationIds().ToHashSet();

        SourceItems.Clear();
        foreach (var s in allSources.Where(s => profileSourceIds.Contains(s.Id)))
            SourceItems.Add(s);

        DestinationItems.Clear();
        foreach (var d in allDests.Where(d => profileDestIds.Contains(d.Id)))
            DestinationItems.Add(d);

        AddLogEntry($"Perfil '{profile.Name}' cargado.", Colors.CornflowerBlue);
    }

    [RelayCommand]
    private async Task DeleteProfile(BackupProfile profile)
    {
        await _databaseService.DeleteProfileAsync(profile.Id);
        Profiles.Remove(profile);
        if (SelectedProfile?.Id == profile.Id) SelectedProfile = null;
    }

    // ── D2: History navigation ─────────────────────────────────────────────

    [RelayCommand]
    private async Task NavigateToHistory()
    {
        var page = _serviceProvider.GetRequiredService<HistoryPage>();
        await _navigationService.NavigateToAsync(page);
    }

    [RelayCommand]
    private async Task NavigateToRestore()
    {
        var page = _serviceProvider.GetRequiredService<RestorePage>();
        await _navigationService.NavigateToAsync(page);
    }

    // ── Backup execution ───────────────────────────────────────────────────

    [RelayCommand]
    private void CancelBackup()
    {
        _backupCts?.Cancel();
        CurrentFileStatus = "Cancelando operación...";
        CanCancel         = false;
    }

    [RelayCommand]
    private async Task StartBackup() => await StartBackupAsync(DestinationItems.FirstOrDefault());

    private async Task StartBackupAsync(BackupDestination? destination)
    {
        var validation = _backupValidator.Validate(SourceItems, DestinationItems);
        if (!validation.IsValid)
        {
            await _navigationService.DisplayAlertAsync("Datos incompletos", validation.Message, "OK");
            return;
        }

        if (destination == null)
        {
            await _navigationService.DisplayAlertAsync("Atención", "No hay un destino seleccionado.", "OK");
            return;
        }

        IsBusy      = true;
        CanCancel   = true;
        ShowActivityLog = true;
        ResetStats();

        _backupCts = new CancellationTokenSource();
        var progressReporter = new Progress<BackupProgressReport>(UpdateProgress);

        var extensions = IncludeExtensions?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        DateTimeOffset? sinceDateOffset = UseSinceDate ? new DateTimeOffset(SinceDate) : null;

        try
        {
            var report = await _backupEngine.ExecuteAsync(
                SourceItems.ToList(),
                destination,
                _backupStrategy,
                ParallelDegree,
                progressReporter,
                _backupCts.Token,
                extensions,
                IsDryRun,
                sinceDateOffset);

            if (report == null) throw new Exception("El motor de backup no generó un reporte.");

            GlobalProgress    = 1.0;
            PercentageText    = "100%";
            CurrentFileStatus = "Finalizado. Preparando reporte...";

            // D2 — Persistir resumen en historial
            await _databaseService.SaveReportSummaryAsync(new BackupReportSummary
            {
                Date            = DateTime.UtcNow,
                TotalCopied     = report.TotalCopied,
                TotalFailed     = report.TotalFailed,
                TotalSkipped    = report.TotalSkipped,
                TotalBytes      = report.TotalBytesCopied,
                DurationSecs    = (int)report.Duration.TotalSeconds,
                WasDryRun       = report.WasDryRun,
                DestinationPath = destination.BackupPath,
                ProfileId       = SelectedProfile?.Id
            });

            await Task.Delay(500);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    await _navigationService.NavigateToAsync(new ReportPage(report));
                }
                catch (Exception navEx)
                {
                    AddLogEntry($"ERROR NAV: {navEx.Message}", Colors.Red);
                    await _navigationService.DisplayAlertAsync("Error de Navegación",
                        $"No se pudo abrir la pantalla de reporte: {navEx.Message}", "OK");
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
            var inner    = ex.InnerException != null ? $"\nInner: {ex.InnerException.Message}" : "";
            var errorMsg = $"Error: {ex.Message}{inner}\nStack: {ex.StackTrace}";

            AddLogEntry($"CRASH: {ex.Message}", Colors.Red);
            if (ex.InnerException != null) AddLogEntry($"INNER: {ex.InnerException.Message}", Colors.Red);
            Console.WriteLine(errorMsg);

            await _navigationService.DisplayAlertAsync("Error de Ejecución",
                $"No pudimos completar el backup:\n{ex.Message}{inner}", "OK");

            CurrentFileStatus = "Error fatal durante la ejecución.";
            ShowActivityLog   = true;
        }
        finally
        {
            IsBusy    = false;
            CanCancel = false;
            _backupCts?.Dispose();
            _backupCts = null;
        }
    }

    // ── Progress / stats ──────────────────────────────────────────────────

    private void ResetStats()
    {
        GlobalProgress    = 0;
        PercentageText    = "0%";
        StatsScanned      = 0;
        StatsCopied       = 0;
        StatsFailed       = 0;
        CurrentFileStatus = IsDryRun ? "Iniciando simulacro..." : "Iniciando...";
        LogItems.Clear();
    }

    private void UpdateProgress(BackupProgressReport report)
    {
        if (!IsBusy && report.Phase != BackupPhase.Completed) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!IsBusy && report.Phase != BackupPhase.Completed) return;

            StatsScanned   = report.ScannedCount;
            StatsCopied    = report.CopiedCount;
            StatsFailed    = report.FailedCount;
            GlobalProgress = report.Progress;
            PercentageText = $"{(int)(report.Progress * 100)}%";

            CurrentFileStatus = report.Phase switch
            {
                BackupPhase.Preparing => IsDryRun ? "Simulacro — preparando..." : "Preparando motores...",
                BackupPhase.Scanning  => $"Analizando: {report.CurrentItem}",
                BackupPhase.Copying   => IsDryRun ? $"Simulando: {report.CurrentItem}" : $"Copiando: {report.CurrentItem}",
                _                     => CurrentFileStatus
            };

            if (!string.IsNullOrEmpty(report.LastErrorMessage))
            {
                var color = report.LastErrorIsWarning ? Colors.Orange : Colors.Red;
                AddLogEntry(report.LastErrorMessage, color);
            }
            else if (report.Phase == BackupPhase.Copying && !string.IsNullOrEmpty(report.CurrentItem))
            {
                AddLogEntry(IsDryRun ? $"Simularía: {report.CurrentItem}" : $"Copiado: {report.CurrentItem}",
                    Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.DimGray);
            }
        });
    }

    private void AddLogEntry(string message, Color color)
    {
        var entry = new LogEntry(message, DateTime.Now.ToString("HH:mm:ss"), color);
        if (LogItems.Count >= 100) LogItems.RemoveAt(0);
        LogItems.Add(entry);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024          => $"{bytes / 1024.0:F1} KB",
        _                => $"{bytes} B"
    };
}






