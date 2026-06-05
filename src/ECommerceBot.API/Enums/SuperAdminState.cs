namespace ECommerceBot.API.Enums;

public enum SuperAdminState
{
    None = 0,
    AwaitingTenantName = 1,
    AwaitingBotToken = 2,
    ConfirmAddTenant = 3,
    AwaitingImpersonateTenantSlug = 4,
}
