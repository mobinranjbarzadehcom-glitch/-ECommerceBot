using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IRenewalRequestRepository : IGenericRepository<RenewalRequest>
{
    Task<IEnumerable<RenewalRequest>> GetByTenantIdAsync(int tenantId);
    Task<IEnumerable<RenewalRequest>> GetPendingAsync();
}
