namespace LocalBackupMaster.Models;

/// <summary>D2 — Resumen ligero de una ejecución para mostrar en el historial.</summary>
public class BackupReportSummary
{
    public int      Id            { get; set; }
    public DateTime Date          { get; set; } = DateTime.UtcNow;
    public int      TotalCopied   { get; set; }
    public int      TotalFailed   { get; set; }
    public int      TotalSkipped  { get; set; }
    public long     TotalBytes    { get; set; }
    public int      DurationSecs  { get; set; }
    public bool     WasDryRun     { get; set; }
    public string?  DestinationPath { get; set; }
    public int?     ProfileId     { get; set; }

    // ── Helpers de presentación ───────────────────────────────────────────
    public string DateText => Date.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public string SizeText => TotalBytes switch
    {
        >= 1_073_741_824 => $"{TotalBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{TotalBytes / 1_048_576.0:F1} MB",
        >= 1024          => $"{TotalBytes / 1024.0:F1} KB",
        _                => $"{TotalBytes} B"
    };

    public string DurationText => DurationSecs < 60
        ? $"{DurationSecs}s"
        : $"{DurationSecs / 60}m {DurationSecs % 60}s";

    public string StatusEmoji => WasDryRun ? "🔍" : TotalFailed > 0 ? "⚠️" : "✅";
}
