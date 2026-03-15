using LocalBackupMaster.ViewModels;

namespace LocalBackupMaster;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    // ─── Ciclo de vida ────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing: {ex.Message}");
        }

        _ = HeaderTitle.FadeTo(1, 600);
        _ = HeaderTitle.TranslateTo(0, 0, 600, Easing.CubicOut);
        _ = SubHeaderLabel.FadeTo(1, 800);

        await Task.Delay(120);
        _ = SourcesCard.FadeTo(1, 500);
        _ = SourcesCard.TranslateTo(0, 0, 500, Easing.CubicOut);

        await Task.Delay(100);
        _ = DestsCard.FadeTo(1, 500);
        _ = DestsCard.TranslateTo(0, 0, 500, Easing.CubicOut);

        await Task.Delay(100);
        _ = ConfigCard.FadeTo(1, 500);
        _ = ConfigCard.TranslateTo(0, 0, 500, Easing.CubicOut);
    }

    // ─── Animations Helpers ──────────────────────────────────────────────────

    private static async Task AnimateButtonClickAsync(View view)
    {
        await view.ScaleTo(0.94, 90, Easing.CubicOut);
        await view.ScaleTo(1.00, 90, Easing.CubicIn);
    }

    // ─── Event Handlers (Solo para Animaciones o disparadores simples) ───────

    private async void OnSelectSourceClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        _viewModel.SelectSourceCommand.Execute(null);
    }

    private async void OnSelectDestinationClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        _viewModel.SelectDestinationCommand.Execute(null);
    }

    private async void OnCancelBackupClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        // El comando se vincula en XAML, esto es opcional si solo queremos animación
    }

    private async void OnStartBackupClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);

        // Activamos la visibilidad de la card de progreso manualmente para la animación inicial
        ProgressCard.IsVisible = true;
        _ = ProgressCard.FadeTo(1, 400);
        _ = ProgressCard.ScaleTo(1, 400, Easing.SpringOut);
    }
}
