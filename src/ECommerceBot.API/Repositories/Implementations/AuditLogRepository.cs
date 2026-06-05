using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class AuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<AuditLog>> GetByAdminIdAsync(int adminId, int limit = 50) =>
        await _dbSet
            .Where(a => a.AdminId == adminId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100) =>
        await _dbSet
            .Include(a => a.Admin)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<IEnumerable<AuditLog>> GetByActionAsync(string action, int limit = 50) =>
        await _dbSet
            .Where(a => a.Action == action)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();
}
