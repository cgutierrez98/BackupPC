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

        // Register Database and Services
        // IDbContextFactory crea un DbContext independiente por operación → thread-safe con hilos paralelos
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "backupmaster.db");
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Filename={dbPath}"));

        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<DeviceWatcherService>();
        builder.Services.AddTransient<IBackupScannerService, BackupScannerService>();

        // Register Backup Engine and Strategy
        builder.Services.AddTransient<IBackupStrategy, IncrementalHashStrategy>();
        builder.Services.AddTransient<IBackupEngine, ParallelBackupEngine>();

        // Phase 3 Support Services
        builder.Services.AddSingleton<INavigationService, MauiNavigationService>();
        builder.Services.AddSingleton<INotificationService, MauiNotificationService>();
        builder.Services.AddSingleton<IBackupValidator, BackupValidator>();
        builder.Services.AddSingleton<IReportExportService, JsonReportExportService>();

        // Register ViewModels
        builder.Services.AddSingleton<MainViewModel>();

        // Register Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ReportPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
