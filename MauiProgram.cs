using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using LocalBackupMaster.Services;
using LocalBackupMaster.ViewModels;
using LocalBackupMaster.Services.Strategies;
using LocalBackupMaster.Services.BackupEngine;
using LocalBackupMaster.Services.Navigation;
using LocalBackupMaster.Services.Validation;
using CommunityToolkit.Maui;

namespace LocalBackupMaster;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ── Database ────────────────────────────────────────────────────────
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "backupmaster.db");
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Filename={dbPath}"));

        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

        // ── Core services ───────────────────────────────────────────────────
        builder.Services.AddSingleton<DeviceWatcherService>();
        builder.Services.AddTransient<IBackupScannerService, BackupScannerService>();

        // ── B1: Encryption ──────────────────────────────────────────────────
        builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

        // ── B2: Integrity check ──────────────────────────────────────────────
        builder.Services.AddTransient<IIntegrityCheckService, IntegrityCheckService>();

        // ── C2: Version cleanup ──────────────────────────────────────────────
        builder.Services.AddTransient<IVersionCleanupService, VersionCleanupService>();

        // ── D3: Restore ─────────────────────────────────────────────────────
        builder.Services.AddTransient<IRestoreService, RestoreService>();

        // ── Backup engine + strategy ─────────────────────────────────────────
        builder.Services.AddTransient<IBackupStrategy, IncrementalHashStrategy>();
        builder.Services.AddTransient<IBackupEngine, ParallelBackupEngine>();

        // ── Phase 3 support services ─────────────────────────────────────────
        builder.Services.AddSingleton<INavigationService, MauiNavigationService>();
        builder.Services.AddSingleton<INotificationService, MauiNotificationService>();
        builder.Services.AddSingleton<IBackupValidator, BackupValidator>();
        builder.Services.AddSingleton<IReportExportService, JsonReportExportService>();

        // ── ViewModels ───────────────────────────────────────────────────────
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<RestoreViewModel>();

        // ── Pages ────────────────────────────────────────────────────────────
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ReportPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<RestorePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
