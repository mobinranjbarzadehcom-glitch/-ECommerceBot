using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Telegram.States;

public class SuperAdminConversation
{
    public SuperAdminState State { get; set; } = SuperAdminState.None;

    // Provisioning wizard
    public string? PendingTenantName { get; set; }
    public string? PendingCustomerName { get; set; }
    public string? PendingCustomerPhone { get; set; }
    public string? PendingCustomerUsername { get; set; }
    public string? PendingBotToken { get; set; }
    public bool PendingIsTrial { get; set; }
    public int PendingPlanId { get; set; }
    public int PendingDurationMonths { get; set; }
    public int PendingTrialDays { get; set; }

    // Suspension
    public int PendingSuspendTenantId { get; set; }

    // CRM notes
    public int PendingNoteTenantId { get; set; }
}
