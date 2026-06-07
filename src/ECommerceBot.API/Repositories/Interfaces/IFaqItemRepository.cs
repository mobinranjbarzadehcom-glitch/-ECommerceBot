using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IFaqItemRepository : IGenericRepository<FaqItem>
{
    Task<IEnumerable<FaqItem>> GetActiveByTenantIdAsync(int tenantId);
    Task<int> GetNextDisplayOrderAsync(int tenantId);
}
