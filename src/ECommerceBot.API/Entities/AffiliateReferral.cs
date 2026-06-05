namespace ECommerceBot.API.Entities;

public class AffiliateReferral : BaseEntity
{
    public int TenantId { get; set; }
    public int AffiliateId { get; set; }
    public int ReferredUserId { get; set; }
    public decimal BonusAmount { get; set; } = 0;
    public DateTime? PaidAt { get; set; }

    public Affiliate Affiliate { get; set; } = null!;
    public TelegramUser ReferredUser { get; set; } = null!;
}
