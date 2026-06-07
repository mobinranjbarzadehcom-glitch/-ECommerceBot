using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class RenewalRequestRepository : GenericRepository<RenewalRequest>, IRenewalRequestRepository
{
    public RenewalRequestRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<RenewalRequest>> GetByTenantIdAsync(int tenantId) =>
        await _dbSet
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<RenewalRequest>> GetPendingAsync() =>
        await _dbSet
            .Include(r => r.Tenant)
            .Where(r => r.Status == RenewalRequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
}
