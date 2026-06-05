using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ECommerceBot.API.Infrastructure.Background;

public class TenantExpiryNotificationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private static readonly int[] NotifyAtDays = { 7, 3, 1 };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantExpiryNotificationService> _logger;

    public TenantExpiryNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<TenantExpiryNotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tenant expiry notification service started");
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SweepAsync(stoppingToken);
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Tenant expiry notification service stopped");
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();

            var now = DateTime.UtcNow;
            // Load tenants expiring within 8 days (widest window) with an owner configured
            var tenants = await db.Tenants
                .Where(t => t.IsActive
                    && t.OwnerTelegramId.HasValue
                    && t.ExpiresAt.HasValue
                    && t.ExpiresAt > now
                    && t.ExpiresAt <= now.AddDays(8))
                .ToListAsync(ct);

            foreach (var tenant in tenants)
            {
                await TrySendReminderAsync(tenant, db, botClient, now, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant expiry notification sweep failed");
        }
    }

    private async Task TrySendReminderAsync(
        Tenant tenant, AppDbContext db, ITelegramBotClient bot,
        DateTime now, CancellationToken ct)
    {
        var daysLeft = (int)Math.Ceiling((tenant.ExpiresAt!.Value - now).TotalDays);
        var sentDays = ParseSent(tenant.ExpiryRemindersSent);

        foreach (var threshold in NotifyAtDays)
        {
            if (daysLeft <= threshold && !sentDays.Contains(threshold))
            {
                try
                {
                    var message = daysLeft <= 1
                        ? $"⚠️ <b>هشدار:</b> اشتراک فروشگاه <b>{tenant.TenantName}</b> فردا منقضی می‌شود!\n\nلطفاً اشتراک خود را تمدید کنید."
                        : $"⏰ <b>یادآوری:</b> اشتراک فروشگاه <b>{tenant.TenantName}</b> تا <b>{daysLeft} روز دیگر</b> منقضی می‌شود.\n\nلطفاً برای تمدید اقدام کنید.";

                    await bot.SendMessage(
                        chatId: tenant.OwnerTelegramId!.Value,
                        text: message,
                        parseMode: ParseMode.Html,
                        cancellationToken: ct);

                    sentDays.Add(threshold);

                    tenant.ExpiryRemindersSent = string.Join(",", sentDays);
                    db.Tenants.Update(tenant);
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Sent {Days}-day expiry reminder to tenant {TenantName} (owner {OwnerId})",
                        daysLeft, tenant.TenantName, tenant.OwnerTelegramId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send expiry reminder to tenant {TenantId}", tenant.Id);
                }

                break; // Send at most one threshold notification per sweep
            }
        }
    }

    private static HashSet<int> ParseSent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToHashSet();
    }
}
