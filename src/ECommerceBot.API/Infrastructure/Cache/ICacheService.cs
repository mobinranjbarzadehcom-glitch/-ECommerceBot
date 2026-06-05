namespace ECommerceBot.API.Infrastructure.Cache;

public interface ICacheService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value, TimeSpan expiry);
    Task RemoveAsync(string key);
}
