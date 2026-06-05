namespace ECommerceBot.API.Infrastructure.Multitenancy;

public interface ITenantContext
{
    int TenantId { get; }
    bool IsSet { get; }
    string? BotToken { get; }
    void SetTenant(int tenantId, string? botToken = null);
}
