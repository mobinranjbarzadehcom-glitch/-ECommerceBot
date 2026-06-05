using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Telegram.States;

public class SuperAdminConversation
{
    public SuperAdminState State { get; set; } = SuperAdminState.None;
    public string? PendingTenantName { get; set; }
    public string? PendingBotToken { get; set; }
}
