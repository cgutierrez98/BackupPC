using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace LocalBackupMaster.Services;

public class MauiNotificationService : INotificationService
{
    public async Task ShowNotificationAsync(string title, string message)
    {
        // Usamos Toast de CommunityToolkit para una notificación ligera (estilo Toast nativo)
        var toast = Toast.Make($"{title}: {message}", ToastDuration.Long);
        await toast.Show();
    }

    public async Task ShowSuccessAsync(string message)
    {
        var toast = Toast.Make($"✅ {message}", ToastDuration.Short);
        await toast.Show();
    }

    public async Task ShowErrorAsync(string message)
    {
        // Para errores, Snackbar es mejor porque permite definir colores o duración (aunque aquí lo mantendremos simple)
        var snackbar = Snackbar.Make($"❌ {message}", duration: TimeSpan.FromSeconds(5));
        await snackbar.Show();
    }
}
