namespace ECommerceBot.API.Services.Interfaces;

public record BotHealthStatus(
    int TenantId,
    string TenantName,
    bool IsOnline,
    string? BotUsername,
    DateTime CheckedAt,
    string? WebhookUrl = null,
    string? WebhookLastError = null,
    int PendingUpdateCount = 0,
    bool WebhookChecked = false);

public interface IBotHealthService
{
    BotHealthStatus? GetStatus(int tenantId);
    IReadOnlyList<BotHealthStatus> GetAllStatuses();
}
