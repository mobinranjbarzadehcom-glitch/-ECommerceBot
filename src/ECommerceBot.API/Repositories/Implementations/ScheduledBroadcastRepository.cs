using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class ScheduledBroadcastRepository : GenericRepository<ScheduledBroadcast>, IScheduledBroadcastRepository
{
    public ScheduledBroadcastRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ScheduledBroadcast>> GetDueAsync(DateTime asOf) =>
        await _dbSet
            .Where(b => b.Status == BroadcastStatus.Pending && b.ScheduledAt <= asOf)
            .OrderBy(b => b.ScheduledAt)
            .ToListAsync();

    public async Task<IEnumerable<ScheduledBroadcast>> GetByTenantIdAsync(int tenantId) =>
        await _dbSet
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

    public async Task ResetStaleRunningJobsAsync()
    {
        var stale = await _dbSet
            .Where(b => b.Status == BroadcastStatus.Running &&
                        b.StartedAt < DateTime.UtcNow.AddHours(-2))
            .ToListAsync();

        foreach (var job in stale)
        {
            job.Status = BroadcastStatus.Failed;
        }
    }
}
