using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Infrastructure.Security;
using ECommerceBot.API.Telegram;
using ECommerceBot.API.Telegram.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace ECommerceBot.API.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> options,
        ILogger<TelegramWebhookController> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Legacy single-tenant endpoint — uses the default tenant from config.</summary>
    [HttpPost("webhook")]
    [EnableRateLimiting("webhook")]
    public IActionResult Webhook([FromBody] Update update, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_options.WebhookSecretToken))
        {
            var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
            if (header != _options.WebhookSecretToken)
            {
                _logger.LogWarning("Invalid webhook secret token from {IP}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }
        }

        // Default tenant: TenantId resolved from DB at dispatch time via TenantContext default
        FireAndForget(update, tenantId: null, botToken: null);
        return Ok();
    }

    /// <summary>Multi-tenant endpoint — resolves tenant by slug.</summary>
    [HttpPost("{tenantSlug}/webhook")]
    [EnableRateLimiting("webhook")]
    public async Task<IActionResult> TenantWebhook(string tenantSlug, [FromBody] Update update, CancellationToken ct)
    {
        // Resolve tenant in the HTTP request scope (no TenantContext filter applies to Tenants table)
        await using var resolveScope = _scopeFactory.CreateAsyncScope();
        var resolver = resolveScope.ServiceProvider.GetRequiredService<ITenantResolver>();
        var tenant = await resolver.ResolveBySlugAsync(tenantSlug, ct);

        if (tenant is null)
        {
            // Distinguish between "slug not found" and "slug found but IsActive=false"
            var anyTenant = await resolver.FindBySlugAsync(tenantSlug, ct);
            if (anyTenant is not null)
                _logger.LogWarning(
                    "Webhook for INACTIVE tenant slug {Slug} — Status={Status} IsActive={IsActive}. " +
                    "Use SuperAdmin panel to activate or retry webhook.",
                    tenantSlug, anyTenant.Status, anyTenant.IsActive);
            else
                _logger.LogWarning("Webhook received for unknown tenant slug: {Slug}", tenantSlug);
            return NotFound();
        }

        if (!string.IsNullOrEmpty(tenant.WebhookSecret))
        {
            var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
            if (header != tenant.WebhookSecret)
            {
                _logger.LogWarning("Invalid webhook secret for tenant {Slug} from {IP}",
                    tenantSlug, HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }
        }

        var aes = resolveScope.ServiceProvider.GetRequiredService<IAesEncryptionService>();
        var botToken = aes.Decrypt(tenant.BotTokenEncrypted);

        FireAndForget(update, tenantId: tenant.Id, botToken: botToken);
        return Ok();
    }

    private void FireAndForget(Update update, int? tenantId, string? botToken)
    {
        var updateId = update.Id;
        _ = Task.Run(async () =>
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            // Set tenant context before any DB queries run
            if (tenantId.HasValue)
            {
                var tenantCtx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantCtx.SetTenant(tenantId.Value, botToken);
            }

            var dispatcher = scope.ServiceProvider.GetRequiredService<IUpdateDispatcher>();
            try
            {
                await dispatcher.DispatchAsync(update, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook update {UpdateId}", updateId);
            }
        }, CancellationToken.None);
    }
}
