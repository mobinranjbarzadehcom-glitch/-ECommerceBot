using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class Tenant : BaseEntity
{
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public string? BotUsername { get; set; }
    public string BotTokenEncrypted { get; set; } = string.Empty;
    public string? WebhookSecret { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.PendingSetup;
    public bool IsActive { get; set; } = true;

    public int? PlanId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool IsInGracePeriod { get; set; } = false;

    public int MaxUsers { get; set; } = 500;
    public int MaxProducts { get; set; } = 50;
    public int MaxAdmins { get; set; } = 2;

    public long? OwnerTelegramId { get; set; }

    public SubscriptionPlan? Plan { get; set; }
}
