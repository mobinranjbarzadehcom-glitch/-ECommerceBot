using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ITenantRepository : IGenericRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug);
    Task<IEnumerable<Tenant>> GetActiveTenantsAsync();
    Task<IEnumerable<Tenant>> GetExpiringTenantsAsync(int withinDays = 7);
    Task<Tenant?> GetByOwnerTelegramIdAsync(long telegramId);
}
