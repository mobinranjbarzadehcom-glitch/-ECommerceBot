using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IScheduledBroadcastRepository : IGenericRepository<ScheduledBroadcast>
{
    Task<IEnumerable<ScheduledBroadcast>> GetDueAsync(DateTime asOf);
    Task<IEnumerable<ScheduledBroadcast>> GetByTenantIdAsync(int tenantId);
    Task ResetStaleRunningJobsAsync();
}
