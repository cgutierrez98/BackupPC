using LocalBackupMaster.Services;
using Microsoft.EntityFrameworkCore;

namespace LocalBackupMaster;

public partial class App : Application
{

    public App(MainPage mainPage, IDbContextFactory<AppDbContext> dbFactory)
    {
        InitializeComponent();

        // Asegurar que la base de datos existe al arrancar
        using (var ctx = dbFactory.CreateDbContext())
        {
            ctx.Database.EnsureCreated();
        }

        MainPage = new NavigationPage(mainPage)
        {
            BarBackgroundColor = Color.FromArgb("#0078D4"),
            BarTextColor = Colors.White
        };
    }
}