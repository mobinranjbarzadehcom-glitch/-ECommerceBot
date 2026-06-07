using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class FaqItemRepository : GenericRepository<FaqItem>, IFaqItemRepository
{
    public FaqItemRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<FaqItem>> GetActiveByTenantIdAsync(int tenantId) =>
        await _dbSet
            .Where(f => f.TenantId == tenantId && f.IsActive)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync();

    public async Task<int> GetNextDisplayOrderAsync(int tenantId)
    {
        var max = await _dbSet
            .Where(f => f.TenantId == tenantId)
            .MaxAsync(f => (int?)f.DisplayOrder);
        return (max ?? 0) + 1;
    }
}
