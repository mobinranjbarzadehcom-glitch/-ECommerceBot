using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IBotSettingRepository : IGenericRepository<BotSetting>
{
    Task<BotSetting?> GetByKeyAsync(string key);
    Task<string?> GetValueAsync(string key);
    Task UpsertAsync(string key, string value, string? description = null);
}
