using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ITenantNoteRepository : IGenericRepository<TenantNote>
{
    Task<IEnumerable<TenantNote>> GetByTenantIdAsync(int tenantId);
}
