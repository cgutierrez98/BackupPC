namespace LocalBackupMaster;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // NavigationPage permite usar Navigation.PushAsync() desde cualquier Page
        return new Window(new NavigationPage(_mainPage)
        {
            BarBackgroundColor = Color.FromArgb("#0078D4"),
            BarTextColor = Colors.White
        });
    }
}