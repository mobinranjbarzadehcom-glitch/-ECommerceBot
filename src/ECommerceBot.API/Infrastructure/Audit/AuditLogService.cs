using ECommerceBot.API.Entities;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Infrastructure.Audit;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IUnitOfWork uow, ILogger<AuditLogService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task LogAsync(int adminId, string action, string? targetType = null, int? targetId = null, string? details = null)
    {
        try
        {
            await _uow.AuditLogs.AddAsync(new AuditLog
            {
                AdminId = adminId,
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Details = details
            });
            await _uow.SaveChangesAsync();

            _logger.LogInformation("Audit: Admin {AdminId} performed {Action} on {TargetType}#{TargetId} — {Details}",
                adminId, action, targetType ?? "—", targetId?.ToString() ?? "—", details ?? "—");
        }
        catch (Exception ex)
        {
            // Audit log failure must never break the primary operation
            _logger.LogError(ex, "Failed to write audit log: {Action} by admin {AdminId}", action, adminId);
        }
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100) =>
        await _uow.AuditLogs.GetRecentAsync(limit);

    public async Task<IEnumerable<AuditLog>> GetByAdminAsync(int adminId, int limit = 50) =>
        await _uow.AuditLogs.GetByAdminIdAsync(adminId, limit);
}
