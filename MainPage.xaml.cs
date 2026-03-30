using LocalBackupMaster.ViewModels;
using LocalBackupMaster.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace LocalBackupMaster;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        AttachHoverScaleEffect(this);
    }

    // ─── Hover effect ────────────────────────────────────────────────────────

    private static void AttachHoverScaleEffect(IVisualTreeElement element)
    {
        if (element is Button btn &&
            !btn.GestureRecognizers.OfType<PointerGestureRecognizer>().Any())
        {
            var pgr = new PointerGestureRecognizer();
            pgr.PointerEntered += (_, _) => btn.ScaleTo(1.04, 100, Easing.CubicOut).SafeFireAndForget();
            pgr.PointerExited  += (_, _) => btn.ScaleTo(1.00, 100, Easing.CubicIn).SafeFireAndForget();
            btn.GestureRecognizers.Add(pgr);
        }
        foreach (var child in element.GetVisualChildren())
            AttachHoverScaleEffect(child);
    }

    // ─── Ciclo de vida ────────────────────────────────────────────────────────

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        OnAppearingAsync().SafeFireAndForget(ex => Console.WriteLine($"OnAppearingAsync error: {ex}"));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    // ─── Reacciones reactivas ────────────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsBusy):
                AnimateProgressCardAsync(_viewModel.IsBusy).SafeFireAndForget();
                break;
            case nameof(MainViewModel.StatsScanned):
                PulseAsync(StatScannedValue).SafeFireAndForget();
                break;
            case nameof(MainViewModel.StatsCopied):
                PulseAsync(StatCopiedValue).SafeFireAndForget();
                break;
            case nameof(MainViewModel.StatsFailed) when _viewModel.StatsFailed > 0:
                PulseAsync(StatFailedValue).SafeFireAndForget();
                break;
        }
    }

    private async Task AnimateProgressCardAsync(bool show)
    {
        if (show)
        {
            ProgressCard.Opacity = 0;
            ProgressCard.Scale   = 0.95;
            ProgressCard.IsVisible = true;
            _ = ProgressCard.FadeTo(1, 280, Easing.CubicOut);
            await ProgressCard.ScaleTo(1, 280, Easing.CubicOut);
        }
        else
        {
            _ = ProgressCard.FadeTo(0, 200, Easing.CubicIn);
            await ProgressCard.ScaleTo(0.97, 200, Easing.CubicIn);
            ProgressCard.IsVisible = false;
        }
    }

    private static async Task PulseAsync(View view)
    {
        await view.ScaleTo(1.20, 110, Easing.CubicOut);
        await view.ScaleTo(1.00, 110, Easing.CubicIn);
    }

    // ─── Inicialización ──────────────────────────────────────────────────────

    private async Task OnAppearingAsync()
    {
        try { await _viewModel.InitializeAsync(); }
        catch (Exception ex) { Console.WriteLine($"InitializeAsync error: {ex.Message}"); }
    }

    // ─── Helpers de animación ────────────────────────────────────────────────

    private static async Task AnimateButtonClickAsync(View view)
    {
        await view.ScaleTo(0.93, 80, Easing.CubicOut);
        await view.ScaleTo(1.00, 80, Easing.CubicIn);
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────

    private void OnSelectSourceClicked(object? sender, EventArgs e) =>
        OnSelectSourceClickedAsync(sender, e).SafeFireAndForget();

    private async Task OnSelectSourceClickedAsync(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        _viewModel.SelectSourceCommand.Execute(null);
    }

    private void OnSelectDestinationClicked(object? sender, EventArgs e) =>
        OnSelectDestinationClickedAsync(sender, e).SafeFireAndForget();

    private async Task OnSelectDestinationClickedAsync(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        _viewModel.SelectDestinationCommand.Execute(null);
    }

    private void OnCancelBackupClicked(object? sender, EventArgs e) =>
        OnCancelBackupClickedAsync(sender, e).SafeFireAndForget();

    private async Task OnCancelBackupClickedAsync(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        var ns = Application.Current?.Handler?.MauiContext?.Services
                   .GetService<LocalBackupMaster.Services.INotificationService>();
        if (ns != null) await ns.ShowNotificationAsync("Backup", "Operación cancelada.");
    }

    private void OnStartBackupClicked(object? sender, EventArgs e) =>
        OnStartBackupClickedAsync(sender, e).SafeFireAndForget();

    private async Task OnStartBackupClickedAsync(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
    }
}
