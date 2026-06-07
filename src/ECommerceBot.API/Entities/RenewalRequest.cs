using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class RenewalRequest : BaseEntity
{
    public int TenantId { get; set; }
    public RenewalRequestType RequestType { get; set; } = RenewalRequestType.Renewal;
    public int DurationMonths { get; set; }
    public int? NewPlanId { get; set; }
    public decimal PriceAmount { get; set; }
    public string? ReceiptFileId { get; set; }
    public RenewalRequestStatus Status { get; set; } = RenewalRequestStatus.Pending;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public long RequesterTelegramId { get; set; }

    public Tenant? Tenant { get; set; }
    public SubscriptionPlan? NewPlan { get; set; }
}
