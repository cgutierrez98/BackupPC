using LocalBackupMaster.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocalBackupMaster;

public partial class App : Application
{
    private readonly IServiceProvider _sp;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public App(IServiceProvider sp, IDbContextFactory<AppDbContext> dbFactory)
    {
        _sp = sp;
        _dbFactory = dbFactory;

        InitializeComponent();          // ← carga Colors.xaml + Styles.xaml

        EnsureDatabase(dbFactory);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // MainPage se resuelve DESPUÉS de InitializeComponent(),
        // así los StaticResource de Colors.xaml ya están disponibles.
        Page root;
        try
        {
            root = _sp.GetRequiredService<MainPage>();
        }
        catch (Exception ex)
        {
            root = new ContentPage
            {
                Title = "Startup Error",
                Content = new ScrollView
                {
                    Content = new Label
                    {
                        Text = $"Error de inicio:\n{ex.Message}\n\n{ex}",
                        Margin = 20, TextColor = Colors.Red
                    }
                }
            };
        }

        return new Window(new NavigationPage(root)
        {
            BarBackgroundColor = Color.FromArgb("#0078D4"),
            BarTextColor = Colors.White
        });
    }

    private static void EnsureDatabase(IDbContextFactory<AppDbContext> dbFactory)
    {
        using var ctx = dbFactory.CreateDbContext();
        ctx.Database.EnsureCreated();
        try
        {
            ctx.Destinations.Select(d => d.AutoBackupOnConnect).Take(0).ToList();
            ctx.Profiles.Take(0).ToList();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
        }
    }
}