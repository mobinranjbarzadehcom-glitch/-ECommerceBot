using ECommerceBot.API.Data;
using ECommerceBot.API.Infrastructure.Backup;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerceBot.API.Services.Implementations;

public class BackupManagementService : IBackupManagementService
{
    private readonly AppDbContext _db;
    private readonly BackupOptions _options;
    private readonly ILogger<BackupManagementService> _logger;

    public BackupManagementService(
        AppDbContext db,
        IOptions<BackupOptions> options,
        ILogger<BackupManagementService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<BackupFileInfo>> TriggerBackupAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return ServiceResult<BackupFileInfo>.Failure("پشتیبان‌گیری در تنظیمات غیرفعال است.");

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"ECommerceBotDb_{timestamp}.bak";
        var backupPath = Path.Combine(_options.Directory, fileName);

        try
        {
            Directory.CreateDirectory(_options.Directory);

            var dbName = _db.Database.GetDbConnection().Database;
            var conn = _db.Database.GetDbConnection();

            await conn.OpenAsync(ct);
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = 1800;
                cmd.CommandText = $"""
                    BACKUP DATABASE [{dbName.Replace("]", "]]")}]
                    TO DISK = N'{backupPath.Replace("'", "''")}'
                    WITH NOFORMAT, NOINIT, COMPRESSION,
                         NAME = N'ECommerceBot Manual Backup {timestamp}',
                         SKIP, NOREWIND, NOUNLOAD, STATS = 10
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            finally
            {
                await conn.CloseAsync();
            }

            var fi = new FileInfo(backupPath);
            var info = new BackupFileInfo(fileName, fi.Exists ? fi.Length : 0L, DateTime.UtcNow);
            _logger.LogInformation("Manual backup created: {File}", fileName);
            return ServiceResult<BackupFileInfo>.Success(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual backup failed");
            return ServiceResult<BackupFileInfo>.Failure($"خطا در پشتیبان‌گیری: {ex.Message}");
        }
    }

    public async Task<ServiceResult> VerifyBackupAsync(string fileName)
    {
        var path = BuildPath(fileName);
        if (path is null || !File.Exists(path))
            return ServiceResult.Failure("فایل بکاپ یافت نشد.");

        try
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = 300;
                cmd.CommandText = $"RESTORE VERIFYONLY FROM DISK = N'{path.Replace("'", "''")}'";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                await conn.CloseAsync();
            }
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup verify failed for {File}", fileName);
            return ServiceResult.Failure($"بکاپ معتبر نیست: {ex.Message}");
        }
    }

    public Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync()
    {
        if (!Directory.Exists(_options.Directory))
            return Task.FromResult<IReadOnlyList<BackupFileInfo>>(Array.Empty<BackupFileInfo>());

        var files = Directory
            .GetFiles(_options.Directory, "ECommerceBotDb_*.bak")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupFileInfo(f.Name, f.Length, f.CreationTimeUtc))
            .ToArray();

        return Task.FromResult<IReadOnlyList<BackupFileInfo>>(files);
    }

    public Task<ServiceResult> DeleteBackupAsync(string fileName)
    {
        var path = BuildPath(fileName);
        if (path is null || !File.Exists(path))
            return Task.FromResult(ServiceResult.Failure("فایل بکاپ یافت نشد."));

        File.Delete(path);
        _logger.LogInformation("Backup deleted: {File}", fileName);
        return Task.FromResult(ServiceResult.Success());
    }

    public Task<string?> GetBackupFullPathAsync(string fileName)
    {
        var path = BuildPath(fileName);
        return Task.FromResult(path is not null && File.Exists(path) ? path : null);
    }

    private string? BuildPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var name = Path.GetFileName(fileName);
        if (!name.StartsWith("ECommerceBotDb_") || !name.EndsWith(".bak")) return null;
        return Path.Combine(_options.Directory, name);
    }
}
