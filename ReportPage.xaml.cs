using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using CommunityToolkit.Maui.Storage;

namespace LocalBackupMaster;

public partial class ReportPage : ContentPage
{
    private readonly BackupReport _report;
    private readonly IReportExportService? _exportService;
    private readonly INotificationService? _notificationService;

    // Color dinámica del banner hero según el resultado
    private static readonly Color ColorSuccess  = Color.FromArgb("#0078D4");
    private static readonly Color ColorWarning  = Color.FromArgb("#D4700A");

    public ReportPage(BackupReport report)
    {
        InitializeComponent();
        _report = report;
        
        // Resolución manual de servicios (MAUI no inyecta automáticamente en constructores de Page si se crean manualmente)
        _exportService = Application.Current?.Handler?.MauiContext?.Services.GetService<IReportExportService>();
        _notificationService = Application.Current?.Handler?.MauiContext?.Services.GetService<INotificationService>();
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
        try
        {
            if (_report == null) return;

            bool hasErrors = _report.TotalFailed > 0;

            // Hero banner
            if (HeroBanner != null)
            {
                HeroBanner.BackgroundColor = hasErrors ? ColorWarning : ColorSuccess;
                ResultEmojiLabel.Text      = _report.ResultEmoji;
                ResultTitleLabel.Text      = _report.ResultText;
                DurationLabel.Text         = $"⏱  Duración: {_report.DurationText}   ·   ⚡ {_report.ParallelDegree} hilos";
                DateLabel.Text             = _report.FinishedAt.ToString("dddd, d MMMM yyyy — HH:mm");
            }

            // Chips
            if (ChipScanned != null)
            {
                ChipScanned.Text = _report.TotalScanned.ToString("N0");
                ChipCopied.Text  = _report.TotalCopied.ToString("N0");
                ChipSize.Text    = _report.SizeText;
                ChipFailed.Text  = _report.TotalFailed.ToString("N0");
            }

            // Lista de archivos con error
            if (hasErrors && FailedList != null && FailedCard != null)
            {
                FailedList.ItemsSource = _report.FailedFiles;
                FailedCard.IsVisible  = true;
            }

            // Lista de archivos copiados (limitamos a 200 para no sobrecargar la UI)
            if (_report.CopiedFiles != null && _report.CopiedFiles.Count > 0 && CopiedList != null && CopiedCard != null)
            {
                var displayList      = _report.CopiedFiles.Take(200).ToList();
                CopiedList.ItemsSource = displayList;
                CopiedCountLabel.Text  = _report.CopiedFiles.Count > 200
                    ? $"(mostrando 200 de {_report.CopiedFiles.Count})"
                    : $"{_report.CopiedFiles.Count} archivo/s";
                CopiedCard.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error populating report: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────
    //  Animación de entrada en cascada
    // ──────────────────────────────────────────────
    private async Task AnimateEntrance()
    {
        try
        {
            // Banner hero
            if (HeroBanner != null)
            {
                HeroBanner.TranslationY = 30;
                HeroBanner.Opacity      = 0;
                _ = HeroBanner.FadeTo(1, 500);
                await HeroBanner.TranslateTo(0, 0, 500, Easing.CubicOut);
            }

            await Task.Delay(100);

            // Chips
            if (StatsGrid != null)
            {
                StatsGrid.Opacity      = 0;
                StatsGrid.TranslationY = 20;
                _ = StatsGrid.FadeTo(1, 400);
                await StatsGrid.TranslateTo(0, 0, 400, Easing.CubicOut);
            }

            await Task.Delay(80);

            if (FailedCard != null && FailedCard.IsVisible)
            {
                FailedCard.Opacity      = 0;
                FailedCard.TranslationY = 20;
                _ = FailedCard.FadeTo(1, 400);
                await FailedCard.TranslateTo(0, 0, 400, Easing.CubicOut);
                await Task.Delay(80);
            }

            if (CopiedCard != null && CopiedCard.IsVisible)
            {
                CopiedCard.Opacity      = 0;
                CopiedCard.TranslationY = 20;
                _ = CopiedCard.FadeTo(1, 400);
                await CopiedCard.TranslateTo(0, 0, 400, Easing.CubicOut);
                await Task.Delay(80);
            }

            if (BackBtn != null)
            {
                _ = BackBtn.FadeTo(1, 350);
            }

            if (ExportBtn != null)
            {
                await ExportBtn.FadeTo(1, 350);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in animations: {ex.Message}");
        }
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

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (sender is View btn)
        {
            await btn.ScaleTo(0.94, 90, Easing.CubicOut);
            await btn.ScaleTo(1.00, 90, Easing.CubicIn);
        }

        if (_exportService == null || _notificationService == null) return;

        try
        {
            var fileName = $"BackupReport_{_report.FinishedAt:yyyyMMdd_HHmm}.json";
            
            // Usamos un stream vacío porque solo queremos la ruta para que la librearía FileSaver abra el diálogo
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(_report)));
            var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);
            
            if (result.IsSuccessful)
            {
                await _notificationService.ShowSuccessAsync("Informe exportado correctamente");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"No se pudo exportar el informe: {ex.Message}", "OK");
        }
    }
}
