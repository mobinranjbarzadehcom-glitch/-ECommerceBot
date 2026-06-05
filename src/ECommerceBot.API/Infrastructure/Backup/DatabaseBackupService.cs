using ECommerceBot.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerceBot.API.Infrastructure.Backup;

public class DatabaseBackupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly BackupOptions _options;

    public DatabaseBackupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseBackupService> logger,
        IOptions<BackupOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Database backup service is disabled. Set Backup:Enabled=true to activate.");
            return;
        }

        _logger.LogInformation(
            "Database backup service started — schedule: every {Hours}h, retention: {Days}d, directory: {Dir}",
            _options.ScheduleHours, _options.RetentionDays, _options.Directory);

        // Allow app to fully start before first backup
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PerformBackupAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.ScheduleHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Database backup service stopped.");
    }

    private async Task PerformBackupAsync(CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"ECommerceBotDb_{timestamp}.bak";
        var backupPath = Path.Combine(_options.Directory, fileName);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Starting database backup → {Path}", backupPath);

        try
        {
            Directory.CreateDirectory(_options.Directory);

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbName = dbContext.Database.GetDbConnection().Database;
            var conn = dbContext.Database.GetDbConnection();

            await conn.OpenAsync(ct);
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = 1800; // 30 min limit for large DBs
                cmd.CommandText = $"""
                    BACKUP DATABASE [{dbName.Replace("]", "]]")}]
                    TO DISK = N'{backupPath.Replace("'", "''")}'
                    WITH NOFORMAT, NOINIT, COMPRESSION,
                         NAME = N'ECommerceBot Full Backup {timestamp}',
                         SKIP, NOREWIND, NOUNLOAD, STATS = 10
                    """;

                await cmd.ExecuteNonQueryAsync(ct);
            }
            finally
            {
                await conn.CloseAsync();
            }

            sw.Stop();
            var sizeBytes = new FileInfo(backupPath).Exists ? new FileInfo(backupPath).Length : 0L;
            _logger.LogInformation(
                "Backup completed in {Elapsed:F1}s — {File} ({Size:F2} MB)",
                sw.Elapsed.TotalSeconds, fileName, sizeBytes / 1_048_576.0);

            await CleanupOldBackupsAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogInformation("Backup cancelled (application shutdown).");
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Never crash the application — backup failure is non-fatal
            _logger.LogError(ex, "Backup failed after {Elapsed:F1}s. Will retry at next scheduled interval.",
                sw.Elapsed.TotalSeconds);
        }
    }

    private Task CleanupOldBackupsAsync()
    {
        try
        {
            if (!Directory.Exists(_options.Directory)) return Task.CompletedTask;

            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            var oldFiles = Directory.GetFiles(_options.Directory, "ECommerceBotDb_*.bak")
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTimeUtc < cutoff)
                .ToList();

            foreach (var file in oldFiles)
            {
                file.Delete();
                _logger.LogInformation("Deleted expired backup: {File}", file.Name);
            }

            if (oldFiles.Count > 0)
                _logger.LogInformation(
                    "Backup cleanup: removed {Count} file(s) older than {Days} days",
                    oldFiles.Count, _options.RetentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup cleanup encountered an error");
        }

        return Task.CompletedTask;
    }
}
