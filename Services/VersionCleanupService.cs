namespace LocalBackupMaster.Services;

/// <summary>C2 — Gestiona versiones anteriores en subdirectorio .bk_versions/.</summary>
public class VersionCleanupService : IVersionCleanupService
{
    private const string VersionsDirName = ".bk_versions";

    public string GetVersionsDir(string destPath, string relPath)
    {
        var dir = Path.GetDirectoryName(relPath) ?? "";
        return Path.Combine(destPath, dir, VersionsDirName);
    }

    public string CreateVersionFileName(string fileName)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
        var ext   = Path.GetExtension(fileName);
        var stem  = Path.GetFileNameWithoutExtension(fileName);
        return $"{stem}_{stamp}{ext}";
    }

    public async Task<int> CleanupAsync(string destPath, string relPath, int maxVersions, CancellationToken ct = default)
    {
        if (maxVersions <= 0) return 0;

        var versionsDir = GetVersionsDir(destPath, relPath);
        if (!Directory.Exists(versionsDir)) return 0;

        var stem    = Path.GetFileNameWithoutExtension(relPath);
        var ext     = Path.GetExtension(relPath);
        var pattern = $"{stem}_*{ext}";

        var versions = Directory
            .GetFiles(versionsDir, pattern)
            .OrderBy(f => f)   // timestamp suffix → lexicographic == chronological
            .ToList();

        int deleted = 0;
        while (versions.Count > maxVersions)
        {
            ct.ThrowIfCancellationRequested();
            try { File.Delete(versions[0]); deleted++; }
            catch { /* best effort */ }
            versions.RemoveAt(0);
        }

        await Task.CompletedTask;
        return deleted;
    }
}
