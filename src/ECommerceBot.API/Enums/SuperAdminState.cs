namespace ECommerceBot.API.Enums;

public enum SuperAdminState
{
    None = 0,
    AwaitingTenantName = 1,
    AwaitingBotToken = 2,
    ConfirmAddTenant = 3,
    AwaitingImpersonateTenantSlug = 4,

    // Extended provisioning wizard
    AwaitingCustomerName = 5,
    AwaitingCustomerPhone = 6,
    AwaitingCustomerUsername = 7,
    AwaitingSubscriptionType = 8,
    AwaitingPlanSelection = 9,
    AwaitingDurationSelection = 10,
    AwaitingTrialDuration = 11,

    // Suspension
    AwaitingSuspensionReason = 12,

    // CRM notes
    AwaitingTenantNote = 13,
}
