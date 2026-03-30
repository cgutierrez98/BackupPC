using System.Text.Json;

namespace LocalBackupMaster.Models;

/// <summary>C1 — Perfil de backup: guarda una configuración reutilizable.</summary>
public class BackupProfile
{
    public int    Id   { get; set; }
    public required string Name { get; set; }

    // IDs de fuentes y destinos asociados (serializado como JSON array)
    public string SourceIdsJson      { get; set; } = "[]";
    public string DestinationIdsJson { get; set; } = "[]";

    // Opciones del motor
    public string? IncludeExtensions { get; set; }   // p.e. ".jpg;.pdf"
    public int     ParallelDegree    { get; set; } = 2;
    public int     ThrottleKBps      { get; set; } = 0;
    public bool    DryRunByDefault   { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Helpers de deserialización ─────────────────────────────────────────
    public List<int> GetSourceIds()
        => JsonSerializer.Deserialize<List<int>>(SourceIdsJson) ?? [];

    public List<int> GetDestinationIds()
        => JsonSerializer.Deserialize<List<int>>(DestinationIdsJson) ?? [];

    public void SetSourceIds(IEnumerable<int> ids)
        => SourceIdsJson = JsonSerializer.Serialize(ids);

    public void SetDestinationIds(IEnumerable<int> ids)
        => DestinationIdsJson = JsonSerializer.Serialize(ids);
}
