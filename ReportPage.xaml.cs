using LocalBackupMaster.Models;

namespace LocalBackupMaster;

public partial class ReportPage : ContentPage
{
    private readonly BackupReport _report;

    // Color dinámica del banner hero según el resultado
    private static readonly Color ColorSuccess  = Color.FromArgb("#0078D4");
    private static readonly Color ColorWarning  = Color.FromArgb("#D4700A");

    public ReportPage(BackupReport report)
    {
        InitializeComponent();
        _report = report;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PopulateReport();
        await AnimateEntrance();
    }

    // ──────────────────────────────────────────────
    //  Rellena todos los controles con los datos del reporte
    // ──────────────────────────────────────────────
    private void PopulateReport()
    {
        bool hasErrors = _report.TotalFailed > 0;

        // Hero banner
        HeroBanner.BackgroundColor = hasErrors ? ColorWarning : ColorSuccess;
        ResultEmojiLabel.Text      = _report.ResultEmoji;
        ResultTitleLabel.Text      = _report.ResultText;
        DurationLabel.Text         = $"⏱  Duración: {_report.DurationText}   ·   ⚡ {_report.ParallelDegree} hilos";
        DateLabel.Text             = _report.FinishedAt.ToString("dddd, d MMMM yyyy — HH:mm");

        // Chips
        ChipScanned.Text = _report.TotalScanned.ToString("N0");
        ChipCopied.Text  = _report.TotalCopied.ToString("N0");
        ChipSize.Text    = _report.SizeText;
        ChipFailed.Text  = _report.TotalFailed.ToString("N0");

        // Lista de archivos con error
        if (hasErrors)
        {
            FailedList.ItemsSource = _report.FailedFiles;
            FailedCard.IsVisible  = true;
        }

        // Lista de archivos copiados (limitamos a 200 para no sobrecargar la UI)
        if (_report.CopiedFiles.Count > 0)
        {
            var displayList      = _report.CopiedFiles.Take(200).ToList();
            CopiedList.ItemsSource = displayList;
            CopiedCountLabel.Text  = _report.CopiedFiles.Count > 200
                ? $"(mostrando 200 de {_report.CopiedFiles.Count})"
                : $"{_report.CopiedFiles.Count} archivo/s";
            CopiedCard.IsVisible = true;
        }
    }

    // ──────────────────────────────────────────────
    //  Animación de entrada en cascada
    // ──────────────────────────────────────────────
    private async Task AnimateEntrance()
    {
        // Banner hero
        HeroBanner.TranslationY = 30;
        HeroBanner.Opacity      = 0;
        _ = HeroBanner.FadeTo(1, 500);
        await HeroBanner.TranslateTo(0, 0, 500, Easing.CubicOut);

        await Task.Delay(100);

        // Chips
        StatsGrid.Opacity      = 0;
        StatsGrid.TranslationY = 20;
        _ = StatsGrid.FadeTo(1, 400);
        await StatsGrid.TranslateTo(0, 0, 400, Easing.CubicOut);

        await Task.Delay(80);

        if (FailedCard.IsVisible)
        {
            FailedCard.Opacity      = 0;
            FailedCard.TranslationY = 20;
            _ = FailedCard.FadeTo(1, 400);
            await FailedCard.TranslateTo(0, 0, 400, Easing.CubicOut);
            await Task.Delay(80);
        }

        if (CopiedCard.IsVisible)
        {
            CopiedCard.Opacity      = 0;
            CopiedCard.TranslationY = 20;
            _ = CopiedCard.FadeTo(1, 400);
            await CopiedCard.TranslateTo(0, 0, 400, Easing.CubicOut);
            await Task.Delay(80);
        }

        await BackBtn.FadeTo(1, 350);
    }

    // ──────────────────────────────────────────────
    //  Botón volver
    // ──────────────────────────────────────────────
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (sender is View btn)
        {
            await btn.ScaleTo(0.94, 90, Easing.CubicOut);
            await btn.ScaleTo(1.00, 90, Easing.CubicIn);
        }
        await Navigation.PopAsync();
    }
}
