namespace ECommerceBot.API.Infrastructure.Backup;

public class BackupOptions
{
    public const string SectionName = "Backup";

    public bool Enabled { get; set; } = false;

    /// <summary>Path to backup directory as seen by SQL Server (shared volume in Docker).</summary>
    public string Directory { get; set; } = "Backups";

    /// <summary>Number of days to keep backup files before auto-deletion.</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Hours between automatic backups.</summary>
    public int ScheduleHours { get; set; } = 24;
}
