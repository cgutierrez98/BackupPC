using LocalBackupMaster.Models;
using LocalBackupMaster.Services;
using CommunityToolkit.Maui.Storage;
using LocalBackupMaster.Helpers;

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
            var exportService = Application.Current?.Handler?.MauiContext?.Services.GetService<IReportExportService>();
            var notificationService = Application.Current?.Handler?.MauiContext?.Services.GetService<INotificationService>();

            if (exportService != null && notificationService != null)
            {
                BindingContext = new LocalBackupMaster.ViewModels.ReportViewModel(report, exportService, notificationService);
            }
            else
            {
                // Mantener compatibilidad: dejar servicios locales por si acaso
                _exportService = exportService;
                _notificationService = notificationService;
            }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        OnAppearingAsync().SafeFireAndForget(ex => Console.WriteLine($"Error in OnAppearing: {ex.Message}"));
    }

    private async Task OnAppearingAsync()
    {
        PopulateReport();
        await AnimateEntrance();
    }

    // ──────────────────────────────────────────────
    //  Rellena todos los controles con los datos del reporte
    // ──────────────────────────────────────────────
    private void PopulateReport()
    {
        // Si ya hay un ViewModel con las propiedades, no manipular la UI desde el code-behind
        if (BindingContext is LocalBackupMaster.ViewModels.ReportViewModel)
            return;

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
    private void OnBackClicked(object? sender, EventArgs e) => OnBackClickedAsync(sender, e).SafeFireAndForget();

    private async Task OnBackClickedAsync(object? sender, EventArgs e)
    {
        if (sender is View btn)
        {
            await btn.ScaleTo(0.94, 90, Easing.CubicOut);
            await btn.ScaleTo(1.00, 90, Easing.CubicIn);
        }
        await Navigation.PopAsync();
    }

    private void OnExportClicked(object? sender, EventArgs e) => OnExportClickedAsync(sender, e).SafeFireAndForget();

    private async Task OnExportClickedAsync(object? sender, EventArgs e)
    {
        if (sender is View btn)
        {
            await btn.ScaleTo(0.94, 90, Easing.CubicOut);
            await btn.ScaleTo(1.00, 90, Easing.CubicIn);
        }

        // Ejecutar en el hilo principal para asegurar acceso a la UI y diálogos
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // 1. Intentar resolver el servicio de notificaciones de varias formas
                var notificationService = _notificationService 
                    ?? Handler?.MauiContext?.Services.GetService<INotificationService>()
                    ?? Application.Current?.Handler?.MauiContext?.Services.GetService<INotificationService>();

                // 2. Preparar el nombre del archivo
                string timeStamp = _report.FinishedAt.ToString("yyyyMMdd_HHmm");
                string fileName = $"BackupReport_{timeStamp}.json";

                // 3. Obtener el JSON (delegamos la serialización al ViewModel si está disponible)
                string json;
                var vm = BindingContext as LocalBackupMaster.ViewModels.ReportViewModel;
                if (vm != null)
                {
                    json = await vm.GetJsonAsync();
                }
                else
                {
                    var options = new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    };
                    json = System.Text.Json.JsonSerializer.Serialize(_report, options);
                }
                
                // 4. Crear el stream del contenido
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                using var stream = new MemoryStream(bytes);

                // 5. Llamar al FileSaver
                var result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);

                if (result != null && result.IsSuccessful)
                {
                    // Dar un respiro para que el foco vuelva de la ventana de guardado de Windows a la app
                    await Task.Delay(300);

                    if (vm != null)
                    {
                        await vm.NotifySavedAsync();
                    }
                    else
                    {
                        await DisplayAlert("Éxito", "Informe exportado correctamente.", "OK");
                    }
                }
                else if (result != null && result.Exception != null)
                {
                    if (vm != null)
                        await vm.NotifyErrorAsync(result.Exception.Message);
                    else
                        await DisplayAlert("Error de Guardado", result.Exception.Message, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error en Exportación", 
                    $"Ocurrió un fallo inesperado: {ex.Message}", 
                    "OK");
            }
        });
    }
}
