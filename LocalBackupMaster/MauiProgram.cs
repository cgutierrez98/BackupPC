using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using LocalBackupMaster.Services;
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

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<DeviceWatcherService>();
        builder.Services.AddTransient<BackupScannerService>();

        // Register Pages
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<ReportPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
