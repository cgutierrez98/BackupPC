namespace LocalBackupMaster.Services.Navigation;

public interface INavigationService
{
    Task NavigateToAsync(Page page);
    Task DisplayAlertAsync(string title, string message, string cancel);
    Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel);
}
