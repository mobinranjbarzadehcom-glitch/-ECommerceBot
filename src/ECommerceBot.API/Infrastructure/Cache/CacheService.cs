using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace ECommerceBot.API.Infrastructure.Cache;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _distributed;
    private readonly IMemoryCache _memory;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache distributed, IMemoryCache memory, ILogger<CacheService> logger)
    {
        _distributed = distributed;
        _memory = memory;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            var bytes = await _distributed.GetAsync(key);
            if (bytes is not null)
                return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache get failed for {Key} — falling back to memory cache", key);
        }

        _memory.TryGetValue(key, out string? memValue);
        return memValue;
    }

    public async Task SetAsync(string key, string value, TimeSpan expiry)
    {
        var opts = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        };

        try
        {
            await _distributed.SetAsync(key, Encoding.UTF8.GetBytes(value), opts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache set failed for {Key} — falling back to memory cache", key);
        }

        // Always write to memory so fallback reads succeed even after Redis failure
        _memory.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry,
            Size = 1
        });
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _distributed.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Distributed cache remove failed for {Key}", key);
        }

        _memory.Remove(key);
    }
}
