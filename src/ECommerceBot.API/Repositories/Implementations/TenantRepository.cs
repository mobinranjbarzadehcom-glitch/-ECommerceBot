using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class TenantRepository : GenericRepository<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext context) : base(context) { }

    public async Task<Tenant?> GetBySlugAsync(string slug) =>
        await _dbSet.FirstOrDefaultAsync(t => t.TenantSlug == slug);

    public async Task<IEnumerable<Tenant>> GetActiveTenantsAsync() =>
        await _dbSet.Where(t => t.IsActive && t.Status == TenantStatus.Active).ToListAsync();

    public async Task<IEnumerable<Tenant>> GetExpiringTenantsAsync(int withinDays = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(withinDays);
        return await _dbSet
            .Where(t => t.IsActive && t.ExpiresAt.HasValue && t.ExpiresAt <= cutoff)
            .ToListAsync();
    }

    public async Task<Tenant?> GetByOwnerTelegramIdAsync(long telegramId) =>
        await _dbSet.FirstOrDefaultAsync(t => t.OwnerTelegramId == telegramId);
}
