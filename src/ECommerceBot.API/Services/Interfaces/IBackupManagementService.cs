using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public record BackupFileInfo(string FileName, long SizeBytes, DateTime CreatedAt);

public interface IBackupManagementService
{
    Task<ServiceResult<BackupFileInfo>> TriggerBackupAsync(CancellationToken ct = default);
    Task<ServiceResult> VerifyBackupAsync(string fileName);
    Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync();
    Task<ServiceResult> DeleteBackupAsync(string fileName);
    Task<string?> GetBackupFullPathAsync(string fileName);
}
