namespace ECommerceBot.API.Infrastructure.Multitenancy;

public class TenantContext : ITenantContext
{
    public int TenantId { get; private set; }
    public bool IsSet { get; private set; }
    public string? BotToken { get; private set; }

    public void SetTenant(int tenantId, string? botToken = null)
    {
        TenantId = tenantId;
        BotToken = botToken;
        IsSet = true;
    }
}
