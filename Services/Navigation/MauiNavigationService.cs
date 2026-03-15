namespace LocalBackupMaster.Services.Navigation;

public class MauiNavigationService : INavigationService
{
    private static INavigation? Navigation => Application.Current?.MainPage?.Navigation;

    public async Task NavigateToAsync(Page page)
    {
        if (Navigation != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    public async Task DisplayAlertAsync(string title, string message, string cancel)
    {
        if (Application.Current?.MainPage != null)
        {
            await Application.Current.MainPage.DisplayAlert(title, message, cancel);
        }
    }

    public async Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
    {
        if (Application.Current?.MainPage != null)
        {
            return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
        }
        return false;
    }
}
