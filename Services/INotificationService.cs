namespace LocalBackupMaster.Services;

public interface INotificationService
{
    Task ShowNotificationAsync(string title, string message);
    Task ShowSuccessAsync(string message);
    Task ShowErrorAsync(string message);
}
