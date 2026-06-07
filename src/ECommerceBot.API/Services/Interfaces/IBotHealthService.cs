namespace ECommerceBot.API.Services.Interfaces;

public record BotHealthStatus(int TenantId, string TenantName, bool IsOnline, string? BotUsername, DateTime CheckedAt);

public interface IBotHealthService
{
    BotHealthStatus? GetStatus(int tenantId);
    IReadOnlyList<BotHealthStatus> GetAllStatuses();
}
