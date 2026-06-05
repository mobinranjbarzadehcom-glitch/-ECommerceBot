using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ECommerceBot.API.Infrastructure.HealthChecks;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _multiplexer;

    public RedisHealthCheck(IServiceProvider serviceProvider)
    {
        _multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (_multiplexer is null)
            return HealthCheckResult.Degraded("Redis is not configured — in-memory cache is active.");

        try
        {
            var latency = await _multiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis reachable. Latency: {latency.TotalMilliseconds:F1} ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
    }
}
