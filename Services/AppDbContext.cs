using System.IO;
using Microsoft.EntityFrameworkCore;
using LocalBackupMaster.Models;
using Microsoft.Maui.Storage;

namespace LocalBackupMaster.Services;

public class AppDbContext : DbContext
{
    public DbSet<BackupSource> Sources { get; set; }
    public DbSet<BackupDestination> Destinations { get; set; }
    public DbSet<FileRecord> FileCatalog { get; set; }

    // Constructor sin parámetros (para migraciones y uso directo)
    public AppDbContext()
    {
    }

    // Constructor para IDbContextFactory (inyección de dependencias)
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "backupmaster.db");
        optionsBuilder.UseSqlite($"Filename={dbPath}");
    }
}
