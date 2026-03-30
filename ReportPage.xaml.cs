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
        AttachHoverScaleEffect(this);
    }

    // ─── Hover effect: scale lift en todos los Button del árbol estático ─────

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
        // Si ya hay un ViewModel activo, los datos van por binding.
        // Solo actualizamos la propiedad visual que no puede ir por binding (color del banner).
        if (BindingContext is LocalBackupMaster.ViewModels.ReportViewModel vm)
        {
            if (HeroBanner != null)
                HeroBanner.BackgroundColor = vm.Report.TotalFailed > 0 ? ColorWarning : ColorSuccess;
            return;
        }

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
            // ── 1. Banner hero: slide-up + fade + leve escala ────────────────
            if (HeroBanner != null)
            {
                HeroBanner.TranslationY = 28;
                HeroBanner.Opacity      = 0;
                HeroBanner.Scale        = 0.97;
                _ = HeroBanner.FadeTo(1, 340, Easing.CubicOut);
                _ = HeroBanner.TranslateTo(0, 0, 340, Easing.CubicOut);
                await HeroBanner.ScaleTo(1, 340, Easing.CubicOut);

                // Rebote del emoji después de que aparece el banner
                if (ResultEmojiLabel != null)
                    BounceEmojiAsync().SafeFireAndForget();
            }

            await Task.Delay(70);

            // ── 2. Chips: cada uno entra escalonado (55 ms entre sí) ─────────
            var chips = new View?[] { ChipScannedBorder, ChipCopiedBorder, ChipSizeBorder, ChipFailedBorder };
            foreach (var chip in chips)
            {
                if (chip == null) continue;
                chip.Opacity      = 0;
                chip.TranslationY = 14;
                chip.Scale        = 0.88;
            }
            // El contenedor ya visible (opacity=1), los hijos animamos individualmente
            if (StatsGrid != null) { StatsGrid.Opacity = 1; StatsGrid.TranslationY = 0; }

            foreach (var chip in chips)
            {
                if (chip == null) continue;
                _ = chip.FadeTo(1, 270, Easing.CubicOut);
                _ = chip.TranslateTo(0, 0, 270, Easing.CubicOut);
                _ = chip.ScaleTo(1, 270, Easing.SpringOut);
                await Task.Delay(55);
            }

            await Task.Delay(50);

            // ── 3. Cards de archivos ──────────────────────────────────────────
            if (FailedCard != null && FailedCard.IsVisible)
            {
                FailedCard.Opacity      = 0;
                FailedCard.TranslationY = 16;
                _ = FailedCard.FadeTo(1, 300, Easing.CubicOut);
                await FailedCard.TranslateTo(0, 0, 300, Easing.CubicOut);
                await Task.Delay(55);
            }

            if (CopiedCard != null && CopiedCard.IsVisible)
            {
                CopiedCard.Opacity      = 0;
                CopiedCard.TranslationY = 16;
                _ = CopiedCard.FadeTo(1, 300, Easing.CubicOut);
                await CopiedCard.TranslateTo(0, 0, 300, Easing.CubicOut);
                await Task.Delay(55);
            }

            // ── 4. Botones: deslizan desde los lados en paralelo ─────────────
            if (BackBtn != null)  { BackBtn.TranslationX  = -18; BackBtn.Opacity  = 0; }
            if (ExportBtn != null) { ExportBtn.TranslationX = 18; ExportBtn.Opacity = 0; }

            if (BackBtn != null)
            {
                _ = BackBtn.FadeTo(1, 260, Easing.CubicOut);
                _ = BackBtn.TranslateTo(0, 0, 260, Easing.CubicOut);
            }
            if (ExportBtn != null)
            {
                _ = ExportBtn.FadeTo(1, 260, Easing.CubicOut);
                _ = ExportBtn.TranslateTo(0, 0, 260, Easing.CubicOut);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in animations: {ex.Message}");
        }
    }

    private async Task BounceEmojiAsync()
    {
        await Task.Delay(260); // deja que el banner aterrice antes
        await ResultEmojiLabel.ScaleTo(1.30, 160, Easing.SpringOut);
        await ResultEmojiLabel.ScaleTo(1.00, 140, Easing.CubicIn);
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
