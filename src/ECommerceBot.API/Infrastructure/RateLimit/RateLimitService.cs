using Microsoft.Extensions.Caching.Memory;

namespace ECommerceBot.API.Infrastructure.RateLimit;

public class RateLimitService : IRateLimitService
{
    private const int UserMessageLimit = 5;
    private static readonly TimeSpan UserMessageWindow = TimeSpan.FromSeconds(10);

    private const int AdminActionLimit = 20;
    private static readonly TimeSpan AdminActionWindow = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;

    public RateLimitService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsRateLimited(long telegramUserId)
    {
        var key = $"rl:msg:{telegramUserId}";
        return CheckAndIncrement(key, UserMessageLimit, UserMessageWindow);
    }

    public bool IsAdminRateLimited(long telegramUserId)
    {
        var key = $"rl:adm:{telegramUserId}";
        return CheckAndIncrement(key, AdminActionLimit, AdminActionWindow);
    }

    private bool CheckAndIncrement(string key, int limit, TimeSpan window)
    {
        // Get or create a counter that expires after the window
        if (!_cache.TryGetValue(key, out int count))
            count = 0;

        count++;

        _cache.Set(key, count, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window,
            Size = 1
        });

        return count > limit;
    }
}
