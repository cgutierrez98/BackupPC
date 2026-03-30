namespace LocalBackupMaster.Models;

public class BackupDestination
{
    public int    Id         { get; set; }
    public required string Uuid { get; set; }
    public string? BackupPath { get; set; }

    // ── Cifrado (B1) ──────────────────────────────────────────────────────────
    public bool    IsEncrypted { get; set; }
    public string? KeyHint     { get; set; }  // Pista visual para el usuario (no es la clave)

    // ── Versionado (C2) ───────────────────────────────────────────────────────
    public bool VersioningEnabled { get; set; }
    public int  MaxVersions       { get; set; } = 5;

    // ── Throttle (A2) ─────────────────────────────────────────────────────────
    public int ThrottleKBps { get; set; } = 0;  // 0 = sin límite

    // ── Auto-backup al conectar (D1) ──────────────────────────────────────────
    public bool AutoBackupOnConnect { get; set; }
}
