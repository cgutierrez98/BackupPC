namespace LocalBackupMaster.Services;

/// <summary>B1 — Cifrado AES-256 opcional por destino.</summary>
public interface IEncryptionService
{
    /// <summary>Cifra el archivo fuente y escribe el resultado cifrado en destPath.</summary>
    Task EncryptFileAsync(string sourcePath, string destPath, string destinationUuid, CancellationToken ct = default);

    /// <summary>Descifra el archivo cifrado de sourcePath y vuelca el plaintext en destPath.</summary>
    Task DecryptFileAsync(string sourcePath, string destPath, string destinationUuid, CancellationToken ct = default);

    /// <summary>Indica si ya existe la clave persistida para ese destino.</summary>
    bool HasKey(string destinationUuid);
}
