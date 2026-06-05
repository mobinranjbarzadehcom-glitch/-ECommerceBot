namespace ECommerceBot.API.Infrastructure.Cache;

public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>Redis connection string (e.g. "localhost:6379"). Null/empty disables Redis.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Key namespace prefix applied to every cache entry.</summary>
    public string InstanceName { get; set; } = "ECommerceBot:";
}
