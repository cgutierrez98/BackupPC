using Microsoft.EntityFrameworkCore;
using LocalBackupMaster.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocalBackupMaster.Services;

/// <summary>
/// Servicio de acceso a datos. Usa IDbContextFactory para crear un DbContext
/// independiente por cada operación, garantizando seguridad en entornos multi-hilo.
/// </summary>
public class DatabaseService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DatabaseService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    // ── Sources ──────────────────────────────────────────────────────────────

    public async Task<BackupSource> AddSourceAsync(string path)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var existing = await ctx.Sources.FirstOrDefaultAsync(s => s.Path == path);
        if (existing != null) return existing;

        var src = new BackupSource { Path = path };
        ctx.Sources.Add(src);
        await ctx.SaveChangesAsync();
        return src;
    }

    public async Task RemoveSourceAsync(int id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var src = await ctx.Sources.FindAsync(id);
        if (src != null)
        {
            ctx.Sources.Remove(src);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<BackupSource>> GetSourcesAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Sources.ToListAsync();
    }

    // ── Destinations ─────────────────────────────────────────────────────────

    public async Task<BackupDestination> AddDestinationAsync(string uuid, string backupPath)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var existing = await ctx.Destinations.FirstOrDefaultAsync(d => d.BackupPath == backupPath);
        if (existing != null) return existing;

        var dest = new BackupDestination { Uuid = uuid, BackupPath = backupPath };
        ctx.Destinations.Add(dest);
        await ctx.SaveChangesAsync();
        return dest;
    }

    public async Task RemoveDestinationAsync(int id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var dest = await ctx.Destinations.FindAsync(id);
        if (dest != null)
        {
            ctx.Destinations.Remove(dest);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<BackupDestination>> GetDestinationsAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Destinations.ToListAsync();
    }

    // ── File Catalog ─────────────────────────────────────────────────────────

    public async Task<FileRecord?> GetFileRecordAsync(int destinationId, string relativePath)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.FileCatalog
            .FirstOrDefaultAsync(f => f.BackupDestinationId == destinationId
                                   && f.RelativePath == relativePath);
    }

    public async Task AddOrUpdateFileRecordAsync(FileRecord record)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var existing = await ctx.FileCatalog
            .FirstOrDefaultAsync(f => f.BackupDestinationId == record.BackupDestinationId
                                    && f.RelativePath == record.RelativePath);

        if (existing == null)
        {
            ctx.FileCatalog.Add(record);
        }
        else
        {
            existing.LastWriteTime = record.LastWriteTime;
            existing.FileSize = record.FileSize;
            existing.FileHash = record.FileHash;
            ctx.FileCatalog.Update(existing);
        }
        await ctx.SaveChangesAsync();
    }
}
