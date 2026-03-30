using System.Security.Cryptography;

namespace LocalBackupMaster.Services;

/// <summary>
/// B1 — Implementación AES-256-CBC.
/// La clave de 32 bytes se genera una vez por destino y se persiste en AppDataDirectory/enc_keys/.
/// El IV aleatorio (16 bytes) se antepone al ciphertext para que cada cifrado sea único.
/// </summary>
public class AesEncryptionService : IEncryptionService
{
    private readonly string _keyDir;

    public AesEncryptionService()
    {
        _keyDir = Path.Combine(FileSystem.AppDataDirectory, "enc_keys");
        Directory.CreateDirectory(_keyDir);
    }

    public bool HasKey(string destinationUuid)
        => File.Exists(KeyPath(destinationUuid));

    public async Task EncryptFileAsync(string sourcePath, string destPath, string destinationUuid, CancellationToken ct = default)
    {
        var key = GetOrCreateKey(destinationUuid);
        var iv  = RandomNumberGenerator.GetBytes(16);

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var input  = File.OpenRead(sourcePath);
        await using var output = File.Create(destPath);

        // IV en cabecera — necesario para descifrar luego
        await output.WriteAsync(iv, ct);

        using var aes = BuildAes(key, iv);
        await using var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await input.CopyToAsync(crypto, ct);
    }

    public async Task DecryptFileAsync(string sourcePath, string destPath, string destinationUuid, CancellationToken ct = default)
    {
        var key = GetOrCreateKey(destinationUuid);

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        await using var input = File.OpenRead(sourcePath);

        var iv   = new byte[16];
        var read = await input.ReadAsync(iv, ct);
        if (read != 16)
            throw new InvalidDataException($"Archivo cifrado corrompido ({sourcePath}): IV incompleto.");

        await using var output = File.Create(destPath);
        using var aes = BuildAes(key, iv);
        await using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        await crypto.CopyToAsync(output, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private byte[] GetOrCreateKey(string uuid)
    {
        var path = KeyPath(uuid);
        if (File.Exists(path))
            return File.ReadAllBytes(path);

        var key = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(path, key);
        return key;
    }

    private string KeyPath(string uuid)
        => Path.Combine(_keyDir, $"{Sanitize(uuid)}.key");

    private static Aes BuildAes(byte[] key, byte[] iv)
    {
        var aes     = Aes.Create();
        aes.Key     = key;
        aes.IV      = iv;
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    private static string Sanitize(string uuid)
        => new(uuid.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
}
