namespace LocalBackupMaster.Models;

public class FileRecord
{
    public int Id { get; set; }
    public required string RelativePath { get; set; }
    public DateTime LastWriteTime { get; set; }
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
    public int BackupDestinationId { get; set; }
    public BackupDestination? Destination { get; set; }

    // ── B1: Cifrado ────────────────────────────────────────────────────────────────────
    public bool IsEncrypted { get; set; }

    // ── C2: Versionado ──────────────────────────────────────────────────────────────
    public int VersionCount { get; set; }
}
