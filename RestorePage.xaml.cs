using LocalBackupMaster.ViewModels;

namespace LocalBackupMaster;

public partial class RestorePage : ContentPage
{
    private readonly RestoreViewModel _vm;

    public RestorePage(RestoreViewModel vm)
    {
        InitializeComponent();
        _vm            = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDestinationsAsync();
    }
}
