using ECommerceBot.API.Infrastructure.Security;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Telegram.Bot;

namespace ECommerceBot.API.Infrastructure.Background;

public class BotHealthBackgroundService : BackgroundService, IBotHealthService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BotHealthBackgroundService> _logger;
    private readonly Dictionary<int, BotHealthStatus> _statuses = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BotHealthBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BotHealthBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public BotHealthStatus? GetStatus(int tenantId) =>
        _statuses.TryGetValue(tenantId, out var s) ? s : null;

    public IReadOnlyList<BotHealthStatus> GetAllStatuses() =>
        _statuses.Values.OrderBy(s => s.TenantName).ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAllBotsAsync(stoppingToken);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CheckAllBotsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var aes = scope.ServiceProvider.GetRequiredService<IAesEncryptionService>();
        var tenants = (await uow.Tenants.GetActiveTenantsAsync()).ToList();

        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested) break;

            string? decryptedToken = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(tenant.BotTokenEncrypted))
                    decryptedToken = aes.Decrypt(tenant.BotTokenEncrypted);
            }
            catch { /* token decryption failure — skip this tenant */ }

            await CheckBotAsync(tenant.Id, tenant.TenantName, decryptedToken, ct);
            await Task.Delay(200, ct);
        }
    }

    private async Task CheckBotAsync(int tenantId, string tenantName, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            SetStatus(tenantId, tenantName, false, null);
            return;
        }

        try
        {
            var client = new TelegramBotClient(token);
            var me = await client.GetMe(ct);
            SetStatus(tenantId, tenantName, true, me.Username);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Bot health check failed for tenant {TenantId}", tenantId);
            SetStatus(tenantId, tenantName, false, null);
        }
    }

    private void SetStatus(int tenantId, string tenantName, bool isOnline, string? username)
    {
        _lock.Wait();
        try
        {
            _statuses[tenantId] = new BotHealthStatus(tenantId, tenantName, isOnline, username, DateTime.UtcNow);
        }
        finally
        {
            _lock.Release();
        }
    }
}
