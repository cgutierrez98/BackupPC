namespace LocalBackupMaster.Models;

public class BackupReport
{
    public DateTime StartedAt    { get; init; } = DateTime.Now;
    public DateTime FinishedAt   { get; init; } = DateTime.Now;

    public int  TotalScanned   { get; init; }
    public int  TotalCopied    { get; init; }
    public int  TotalSkipped   { get; init; }
    public int  TotalFailed    { get; init; }
    public long TotalBytesCopied { get; init; }

    public int  ParallelDegree { get; init; }

    // ── A1: Dry-Run ─────────────────────────────────────────────────────────────────────
    public bool WasDryRun { get; init; }

    public List<string> FailedFiles   { get; init; } = [];
    public List<string> CopiedFiles   { get; init; } = [];

    // Helpers
    public TimeSpan Duration        => FinishedAt - StartedAt;
    public string   DurationText    => Duration.TotalSeconds < 60
        ? $"{(int)Duration.TotalSeconds}s"
        : $"{(int)Duration.TotalMinutes}m {Duration.Seconds}s";

    public string SizeText => TotalBytesCopied switch
    {
        >= 1_073_741_824 => $"{TotalBytesCopied / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{TotalBytesCopied / 1_048_576.0:F1} MB",
        >= 1024          => $"{TotalBytesCopied / 1024.0:F1} KB",
        _                => $"{TotalBytesCopied} B"
    };

    public string ResultEmoji => WasDryRun ? "🔍" : TotalFailed > 0 ? "⚠️" : "✅";
    public string ResultText  => WasDryRun
        ? "Simulacro completado"
        : TotalFailed > 0 ? "Completado con advertencias" : "Completado exitosamente";

    // Chip text helpers para ReportPage/ReportViewModel
    public string ChipScanned => TotalScanned.ToString("N0");
    public string ChipCopied  => TotalCopied.ToString("N0");
    public string ChipFailed  => TotalFailed.ToString("N0");
    public string DateText     => FinishedAt.ToString("dddd, d MMMM yyyy — HH:mm");
}
