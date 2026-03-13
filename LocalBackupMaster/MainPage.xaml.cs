using System.Collections.ObjectModel;
using System.Threading.Channels;
using CommunityToolkit.Maui.Storage;
using LocalBackupMaster.Models;
using LocalBackupMaster.Services;

namespace LocalBackupMaster;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly DeviceWatcherService _deviceWatcherService;
    private readonly BackupScannerService _scannerService;

    public ObservableCollection<BackupSource> SourceItems { get; } = [];
    public ObservableCollection<BackupDestination> DestinationItems { get; } = [];

    private CancellationTokenSource? _backupCts;
    private int _parallelDegree = 4;

    public MainPage(DatabaseService databaseService,
                    DeviceWatcherService deviceWatcherService,
                    BackupScannerService scannerService)
    {
        InitializeComponent();
        BindingContext = this;

        _databaseService = databaseService;
        _deviceWatcherService = deviceWatcherService;
        _scannerService = scannerService;

        _deviceWatcherService.DeviceConnected += OnDeviceConnected;
        _deviceWatcherService.DeviceDisconnected += OnDeviceDisconnected;
        _deviceWatcherService.StartWatching();
    }

    // ─── Ciclo de vida ────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExistingConfigAsync();

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

    private async Task LoadExistingConfigAsync()
    {
        try
        {
            SourceItems.Clear();
            foreach (var s in await _databaseService.GetSourcesAsync())
                SourceItems.Add(s);

            DestinationItems.Clear();
            foreach (var d in await _databaseService.GetDestinationsAsync())
                DestinationItems.Add(d);
        }
        catch { /* sin crash en carga inicial */ }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void OnParallelismChanged(object? sender, ValueChangedEventArgs e)
    {
        _parallelDegree = (int)e.NewValue;
        ParallelismLabel.Text = _parallelDegree.ToString();
    }

    private static async Task AnimateButtonClickAsync(View view)
    {
        await view.ScaleTo(0.94, 90, Easing.CubicOut);
        await view.ScaleTo(1.00, 90, Easing.CubicIn);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };

    // ─── Dispositivo ─────────────────────────────────────────────────────────

    private void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        Dispatcher.Dispatch(async () =>
        {
            bool answer = await DisplayAlert("Nuevo disco detectado",
                $"¿Deseas registrar '{e.VolumeLabel} ({e.DrivePath})' como unidad de destino?",
                "Sí, registrar", "No");

            if (answer)
            {
                var uuid = e.VolumeLabel + "_" + Guid.NewGuid().ToString()[..4];
                var dest = await _databaseService.AddDestinationAsync(uuid, e.DrivePath);
                DestinationItems.Add(dest);
            }
        });
    }

    private void OnDeviceDisconnected(object? sender, DeviceEventArgs e)
        => Dispatcher.Dispatch(() => Console.WriteLine($"Disco desconectado: {e.DrivePath}"));

    // ─── Orígenes ────────────────────────────────────────────────────────────

    private async void OnSelectSourceClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful) return;

        var folderPath = result.Folder.Path;
        if (SourceItems.Any(s => s.Path == folderPath)) return;

        var source = await _databaseService.AddSourceAsync(folderPath);
        SourceItems.Add(source);
    }

    private async void OnRemoveSourceClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is BackupSource src)
        {
            SourceItems.Remove(src);
            await _databaseService.RemoveSourceAsync(src.Id);
        }
    }

    // ─── Destinos ────────────────────────────────────────────────────────────

    private async void OnSelectDestinationClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful) return;

        var folderPath = result.Folder.Path;
        if (DestinationItems.Any(d => d.BackupPath == folderPath)) return;

        var uuid = Guid.NewGuid().ToString();
        var dest = await _databaseService.AddDestinationAsync(uuid, folderPath);
        DestinationItems.Add(dest);
    }

    private async void OnRemoveDestinationClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is BackupDestination dst)
        {
            DestinationItems.Remove(dst);
            await _databaseService.RemoveDestinationAsync(dst.Id);
        }
    }

    // ─── Cancelar ────────────────────────────────────────────────────────────

    private async void OnCancelBackupClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);
        _backupCts?.Cancel();
        CurrentFileLabel.Text = "Cancelando operación...";
        CancelBackupBtn.IsEnabled = false;
    }

    // ─── BACKUP PRINCIPAL ─────────────────────────────────────────────────────

    private async void OnStartBackupClicked(object? sender, EventArgs e)
    {
        if (sender is View btn) await AnimateButtonClickAsync(btn);

        if (SourceItems.Count == 0 || DestinationItems.Count == 0)
        {
            await DisplayAlert("Faltan Datos", "Añade al menos un origen y un destino.", "OK");
            return;
        }

        // ── Snapshot de las colecciones en hilo de UI (antes de entrar al Task.Run) ──
        var sources = SourceItems.ToList();
        var dest = DestinationItems.First();

        if (string.IsNullOrEmpty(dest.BackupPath))
        {
            await DisplayAlert("Error", "La ruta de destino está vacía.", "OK");
            return;
        }

        // ── Preparar UI ──
        StartBackupBtn.IsEnabled = false;
        StartBackupBtn.BackgroundColor = Colors.Gray;
        CancelBackupBtn.IsVisible = true;
        CancelBackupBtn.IsEnabled = true;
        CancelBackupBtn.Opacity = 0;
        _ = CancelBackupBtn.FadeTo(1, 350);

        GlobalProgressBar.Progress = 0;
        PercentageLabel.Text = "0%";
        StatsScannedLabel.Text = "0";
        StatsCopiedLabel.Text = "0";
        StatsFailedLabel.Text = "0";
        CurrentFileLabel.Text = "Pre-escaneando archivos...";

        ProgressCard.IsVisible = true;
        _ = ProgressCard.FadeTo(1, 400);
        _ = ProgressCard.ScaleTo(1, 400, Easing.SpringOut);

        _backupCts = new CancellationTokenSource();
        var token = _backupCts.Token;

        try
        {
            // ── Pre-escaneo ──
            var (totalFiles, totalBytes) = await _scannerService
                .GetTotalFilesAndSizeAsync(sources.Select(s => s.Path), token);

            CurrentFileLabel.Text = $"Pre-escaneo: {totalFiles} archivos ({FormatBytes(totalBytes)})";

            long processedBytes = 0;
            int scannedCount = 0;
            int copiedCount = 0;
            long copiedBytes = 0;
            var failedFiles = new List<string>();
            var copiedFileNames = new List<string>();
            var startedAt = DateTime.Now;

            // ── Channel bounded para productor/consumidores ──
            var channel = Channel.CreateBounded<(FileInfo fi, string relPath, string dstPath)>(
                new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait });

            // ── PRODUCTOR ─────────────────────────────────────────────────────
            // Corre en thread pool. Usa `sources` y `dest` (snapshots de UI).
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var source in sources)
                    {
                        if (!Directory.Exists(source.Path))
                        {
                            Console.WriteLine($"[Producer] Ruta no existe: {source.Path}");
                            continue;
                        }

                        await foreach (var fi in _scannerService.EnumerateFilesAsync(source.Path, token))
                        {
                            token.ThrowIfCancellationRequested();

                            string relPath = Path.GetRelativePath(source.Path, fi.FullName);
                            string dstPath = Path.Combine(dest.BackupPath!, relPath);

                            // Comprobar catálogo
                            FileRecord? existing = await _databaseService.GetFileRecordAsync(dest.Id, relPath);

                            bool dstExists = File.Exists(dstPath);
                            bool needsCopy = existing is null
                                          || !dstExists
                                          || existing.FileSize != fi.Length
                                          || existing.LastWriteTime < fi.LastWriteTimeUtc;

                            // Si el archivo ya existe en destino, pero la metadata sugiere cambios,
                            // validamos con Hash para evitar copias innecesarias si el contenido no varió.
                            // Si NO existe en destino (!dstExists), 'needsCopy' se mantiene true.
                            if (needsCopy && existing is not null && dstExists)
                            {
                                string h = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
                                needsCopy = h != existing.FileHash;
                            }

                            int sc = Interlocked.Increment(ref scannedCount);
                            Interlocked.Add(ref processedBytes, fi.Length);

                            Dispatcher.Dispatch(() =>
                            {
                                StatsScannedLabel.Text = sc.ToString();
                                if (totalBytes > 0)
                                {
                                    double p = Math.Min(1.0, (double)Interlocked.Read(ref processedBytes) / totalBytes);
                                    GlobalProgressBar.Progress = p;
                                    PercentageLabel.Text = $"{(int)(p * 100)}%";
                                }
                                CurrentFileLabel.Text = $"Analizando: {fi.Name}";
                            });

                            if (needsCopy)
                            {
                                Console.WriteLine($"[Producer] Encolando: {relPath}");
                                await channel.Writer.WriteAsync((fi, relPath, dstPath), token);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Console.WriteLine($"[Producer ERROR] {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
                finally
                {
                    channel.Writer.TryComplete();
                }
            }, token);

            // ── CONSUMIDORES paralelos ─────────────────────────────────────────
            var consumerTasks = Enumerable.Range(0, _parallelDegree).Select(i => Task.Run(async () =>
            {
                Console.WriteLine($"[Consumer {i}] Iniciado.");
                await foreach (var (fi, relPath, dstPath) in channel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        string? dir = Path.GetDirectoryName(dstPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                        Console.WriteLine($"[Consumer {i}] Copiando: {fi.Name} → {dstPath}");
                        var copyResult = await _scannerService.TryCopyWithRetryAsync(fi.FullName, dstPath, token);

                        if (copyResult.Success)
                        {
                            string hash = await _scannerService.CalculateXXHash64Async(fi.FullName, token);
                            await _databaseService.AddOrUpdateFileRecordAsync(new FileRecord
                            {
                                RelativePath = relPath,
                                LastWriteTime = fi.LastWriteTimeUtc,
                                FileSize = fi.Length,
                                FileHash = hash,
                                BackupDestinationId = dest.Id
                            });

                            Interlocked.Add(ref copiedBytes, fi.Length);
                            int c = Interlocked.Increment(ref copiedCount);
                            lock (copiedFileNames) copiedFileNames.Add(fi.Name);
                            Dispatcher.Dispatch(() =>
                            {
                                StatsCopiedLabel.Text = c.ToString();
                                CurrentFileLabel.Text = $"Copiado: {fi.Name}";
                            });
                        }
                        else
                        {
                            string reason = $"{fi.Name}: {copyResult.FailReason}";
                            Console.WriteLine($"[Consumer {i}] FALLO: {reason}");
                            lock (failedFiles) failedFiles.Add(reason);
                            int f = failedFiles.Count;
                            Dispatcher.Dispatch(() => StatsFailedLabel.Text = f.ToString());
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        string reason = $"{fi.Name}: {ex.Message}";
                        Console.WriteLine($"[Consumer {i}] EXCEPCIÓN: {ex}");
                        lock (failedFiles) failedFiles.Add(reason);
                    }
                }
                Console.WriteLine($"[Consumer {i}] Finalizado.");
            }, token)).ToArray();

            await producerTask;
            await Task.WhenAll(consumerTasks);

            // ── Finalizar UI ──
            Dispatcher.Dispatch(() =>
            {
                GlobalProgressBar.Progress = 1.0;
                PercentageLabel.Text = "100%";
                CurrentFileLabel.Text = "¡Backup completado!";
            });

            var report = new BackupReport
            {
                StartedAt = startedAt,
                FinishedAt = DateTime.Now,
                TotalScanned = scannedCount,
                TotalCopied = copiedCount,
                TotalSkipped = Math.Max(0, scannedCount - copiedCount - failedFiles.Count),
                TotalFailed = failedFiles.Count,
                TotalBytesCopied = copiedBytes,
                ParallelDegree = _parallelDegree,
                FailedFiles = failedFiles,
                CopiedFiles = copiedFileNames
            };

            await Navigation.PushAsync(new ReportPage(report));
        }
        catch (OperationCanceledException)
        {
            Dispatcher.Dispatch(() => CurrentFileLabel.Text = "Backup cancelado por el usuario.");
            await DisplayAlert("Cancelado", "La operación fue detenida.", "OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Backup ERROR] {ex}");
            await DisplayAlert("Error", $"No pudimos completar el backup:\n{ex.Message}", "OK");
            Dispatcher.Dispatch(() => CurrentFileLabel.Text = "Error durante el backup.");
        }
        finally
        {
            StartBackupBtn.IsEnabled = true;
            StartBackupBtn.BackgroundColor = Color.FromArgb("#0078D4");

            _ = CancelBackupBtn.FadeTo(0, 280).ContinueWith(_ =>
                Dispatcher.Dispatch(() => CancelBackupBtn.IsVisible = false));

            _backupCts?.Dispose();
            _backupCts = null;
        }
    }
}
