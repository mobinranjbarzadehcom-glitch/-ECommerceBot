namespace ECommerceBot.API.Infrastructure.RateLimit;

public interface IRateLimitService
{
    /// <summary>Returns true if the user has exceeded the message rate limit (5 per 10 seconds).</summary>
    bool IsRateLimited(long telegramUserId);

    /// <summary>Returns true if the admin has exceeded the sensitive-action rate limit (20 per minute).</summary>
    bool IsAdminRateLimited(long telegramUserId);
}
