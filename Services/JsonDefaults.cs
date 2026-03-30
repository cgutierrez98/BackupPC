using System.Text.Json;

namespace LocalBackupMaster.Services;

/// <summary>
/// Opciones de serialización JSON compartidas en toda la aplicación.
/// Centralizar aquí evita que los ajustes queden duplicados y desincronizados.
/// </summary>
internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
