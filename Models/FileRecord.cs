using System;

namespace LocalBackupMaster.Models;

public class FileRecord
{
    public int Id { get; set; }
    public required string RelativePath { get; set; }
    public DateTime LastWriteTime { get; set; }
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
    
    // Foreign key to BackupDestination to keep track of files per destination
    public int BackupDestinationId { get; set; }
    public BackupDestination? Destination { get; set; }
}
