using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Infrastructure.Security;
using ECommerceBot.API.UnitOfWork;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ECommerceBot.API.Infrastructure.Background;

public class BroadcastSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BroadcastSchedulerService> _logger;

    public BroadcastSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<BroadcastSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDueJobsAsync(stoppingToken);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var aes = scope.ServiceProvider.GetRequiredService<IAesEncryptionService>();

        await uow.ScheduledBroadcasts.ResetStaleRunningJobsAsync();

        var due = (await uow.ScheduledBroadcasts.GetDueAsync(DateTime.UtcNow)).ToList();
        if (due.Count == 0) return;

        _logger.LogInformation("BroadcastScheduler: {Count} due job(s) found", due.Count);

        foreach (var job in due)
        {
            if (ct.IsCancellationRequested) break;

            job.Status = BroadcastStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            uow.ScheduledBroadcasts.Update(job);
            await uow.SaveChangesAsync(ct);

            try
            {
                var tenant = await uow.Tenants.GetByIdAsync(job.TenantId);
                if (tenant is null || string.IsNullOrWhiteSpace(tenant.BotTokenEncrypted))
                {
                    job.Status = BroadcastStatus.Failed;
                    uow.ScheduledBroadcasts.Update(job);
                    await uow.SaveChangesAsync(ct);
                    continue;
                }

                string decryptedToken;
                try { decryptedToken = aes.Decrypt(tenant.BotTokenEncrypted); }
                catch { job.Status = BroadcastStatus.Failed; uow.ScheduledBroadcasts.Update(job); await uow.SaveChangesAsync(ct); continue; }

                var client = new TelegramBotClient(decryptedToken);

                var users = await uow.Users.FindAsync(u =>
                    u.TenantId == job.TenantId &&
                    !u.IsBlocked &&
                    u.ChatId > 0 &&
                    (job.TargetFilter == BroadcastTargetFilter.All ||
                     (job.TargetFilter == BroadcastTargetFilter.ActiveLast7Days &&
                      u.LastActivity >= DateTime.UtcNow.AddDays(-7)) ||
                     (job.TargetFilter == BroadcastTargetFilter.ActiveLast30Days &&
                      u.LastActivity >= DateTime.UtcNow.AddDays(-30))));

                int sent = 0, failed = 0;
                foreach (var user in users)
                {
                    if (ct.IsCancellationRequested) break;
                    // Re-check if job was cancelled
                    var current = await uow.ScheduledBroadcasts.GetByIdAsync(job.Id);
                    if (current?.Status == BroadcastStatus.Cancelled) break;

                    try
                    {
                        await client.SendMessage(user.ChatId, job.HtmlMessage, parseMode: ParseMode.Html, cancellationToken: ct);
                        sent++;
                        await Task.Delay(35, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                    catch { failed++; }
                }

                var latestJob = await uow.ScheduledBroadcasts.GetByIdAsync(job.Id);
                if (latestJob is not null && latestJob.Status != BroadcastStatus.Cancelled)
                {
                    latestJob.Status = BroadcastStatus.Sent;
                    latestJob.SentCount = sent;
                    latestJob.FailedCount = failed;
                    latestJob.CompletedAt = DateTime.UtcNow;
                    uow.ScheduledBroadcasts.Update(latestJob);
                    await uow.SaveChangesAsync(ct);
                }

                _logger.LogInformation("ScheduledBroadcast {Id}: sent={Sent} failed={Failed}", job.Id, sent, failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledBroadcast {Id} failed", job.Id);
                var latestJob = await uow.ScheduledBroadcasts.GetByIdAsync(job.Id);
                if (latestJob is not null)
                {
                    latestJob.Status = BroadcastStatus.Failed;
                    uow.ScheduledBroadcasts.Update(latestJob);
                    await uow.SaveChangesAsync(ct);
                }
            }
        }
    }
}
