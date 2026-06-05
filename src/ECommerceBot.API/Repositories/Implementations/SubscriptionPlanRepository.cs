using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class SubscriptionPlanRepository : GenericRepository<SubscriptionPlan>, ISubscriptionPlanRepository
{
    public SubscriptionPlanRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<SubscriptionPlan>> GetActivePlansAsync() =>
        await _dbSet.Where(p => p.IsActive).OrderBy(p => p.Tier).ToListAsync();
}
