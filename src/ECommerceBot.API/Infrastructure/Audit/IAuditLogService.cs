using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Infrastructure.Audit;

public interface IAuditLogService
{
    Task LogAsync(int adminId, string action, string? targetType = null, int? targetId = null, string? details = null);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100);
    Task<IEnumerable<AuditLog>> GetByAdminAsync(int adminId, int limit = 50);
}
