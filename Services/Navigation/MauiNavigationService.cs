namespace LocalBackupMaster.Services.Navigation;

public class MauiNavigationService : INavigationService
{
    private static Page? MainPage => Application.Current?.Windows[0].Page;

    public async Task NavigateToAsync(Page page)
    {
        if (MainPage?.Navigation != null)
        {
            await MainPage.Navigation.PushAsync(page);
        }
    }

    public async Task DisplayAlertAsync(string title, string message, string cancel)
    {
        if (MainPage != null)
        {
            await MainPage.DisplayAlert(title, message, cancel);
        }
    }

    public async Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
    {
        if (MainPage != null)
        {
            return await MainPage.DisplayAlert(title, message, accept, cancel);
        }
        return false;
    }
}
