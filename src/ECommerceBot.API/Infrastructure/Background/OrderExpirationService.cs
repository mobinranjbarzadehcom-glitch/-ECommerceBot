using ECommerceBot.API.Infrastructure.Audit;
using ECommerceBot.API.Services.Interfaces;

namespace ECommerceBot.API.Infrastructure.Background;

public class OrderExpirationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderExpirationService> _logger;

    public OrderExpirationService(IServiceScopeFactory scopeFactory, ILogger<OrderExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Order expiration service started. Check interval: {Interval} min", CheckInterval.TotalMinutes);

        // Brief delay so all scoped services are fully registered before first sweep
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepExpiredOrdersAsync(stoppingToken);

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Order expiration service stopped.");
    }

    private async Task SweepExpiredOrdersAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

            var count = await orderService.ExpireStaleOrdersAsync();

            if (count > 0)
            {
                _logger.LogInformation("Expired {Count} stale order(s)", count);
                await auditLog.LogAsync(
                    adminId: 0,
                    action: AuditAction.ExpireOrder,
                    targetType: "Order",
                    targetId: null,
                    details: $"Batch expiration: {count} order(s) expired by background service");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order expiration sweep failed");
        }
    }
}
