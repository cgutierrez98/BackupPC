namespace LocalBackupMaster.Models;

public class BackupDestination
{
    public int Id { get; set; }
    public required string Uuid { get; set; }
    public string? BackupPath { get; set; }
}
