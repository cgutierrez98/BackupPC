using System.IO;
using Microsoft.EntityFrameworkCore;
using LocalBackupMaster.Models;
using Microsoft.Maui.Storage;

namespace LocalBackupMaster.Services;

public class AppDbContext : DbContext
{
    public DbSet<BackupSource>         Sources       { get; set; }
    public DbSet<BackupDestination>    Destinations  { get; set; }
    public DbSet<FileRecord>           FileCatalog   { get; set; }

    // ── C1: Perfiles ──────────────────────────────────────────────────────────
    public DbSet<BackupProfile>        Profiles      { get; set; }

    // ── D2: Historial ─────────────────────────────────────────────────────────
    public DbSet<BackupReportSummary>  ReportHistory { get; set; }

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
        if (optionsBuilder.IsConfigured) return;
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "backupmaster.db");
        optionsBuilder.UseSqlite($"Filename={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // EnsureCreated genera las tablas nuevas automáticamente en SQLite
        modelBuilder.Entity<BackupProfile>().HasKey(p => p.Id);
        modelBuilder.Entity<BackupReportSummary>().HasKey(r => r.Id);
    }
}
