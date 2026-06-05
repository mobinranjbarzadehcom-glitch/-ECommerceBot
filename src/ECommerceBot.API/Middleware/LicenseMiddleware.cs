using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Licensing;
using Microsoft.Extensions.Options;

namespace ECommerceBot.API.Middleware;

/// <summary>
/// Blocks the Telegram webhook endpoint in production when the license is invalid.
/// Health-check endpoints are always allowed through.
/// In Development, only a warning is logged.
/// </summary>
public class LicenseMiddleware
{
    private static readonly PathString[] AlwaysAllowed =
    {
        "/health", "/health/live", "/health/ready"
    };

    private readonly RequestDelegate _next;
    private readonly LicenseStatusCache _cache;
    private readonly LicenseOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<LicenseMiddleware> _logger;

    public LicenseMiddleware(
        RequestDelegate next,
        LicenseStatusCache cache,
        IOptions<LicenseOptions> options,
        IHostEnvironment env,
        ILogger<LicenseMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!_options.Enabled || AlwaysAllowed.Any(p => ctx.Request.Path.StartsWithSegments(p)))
        {
            await _next(ctx);
            return;
        }

        var result = _cache.Result;

        if (!result.IsValid)
        {
            var msg = $"License invalid: {result.Status} — {result.Message}";

            if (_env.IsProduction() && _options.RequireValidLicenseInProduction)
            {
                _logger.LogCritical("{LicenseError}", msg);
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    $"{{\"error\":\"service_unavailable\",\"detail\":\"License validation required.\",\"status\":\"{result.Status}\"}}");
                return;
            }

            _logger.LogWarning("{LicenseError}", msg);
        }

        await _next(ctx);
    }
}
