using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByAdminIdAsync(int adminId, int limit = 50);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100);
    Task<IEnumerable<AuditLog>> GetByActionAsync(string action, int limit = 50);
}
