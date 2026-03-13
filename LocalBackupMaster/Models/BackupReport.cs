namespace LocalBackupMaster.Models;

/// <summary>
/// Resultado completo de una operación de backup, pasado a la pantalla de reporte.
/// </summary>
public class BackupReport
{
    public DateTime StartedAt    { get; init; } = DateTime.Now;
    public DateTime FinishedAt   { get; init; } = DateTime.Now;

    public int  TotalScanned   { get; init; }
    public int  TotalCopied    { get; init; }
    public int  TotalSkipped   { get; init; }   // sin cambios, no necesitaron copia
    public int  TotalFailed    { get; init; }
    public long TotalBytesCopied { get; init; } // bytes efectivamente copiados

    public int  ParallelDegree { get; init; }

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

    public string ResultEmoji => TotalFailed > 0 ? "⚠️" : "✅";
    public string ResultText  => TotalFailed > 0 ? "Completado con advertencias" : "Completado exitosamente";
}
