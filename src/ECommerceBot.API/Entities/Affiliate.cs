namespace ECommerceBot.API.Entities;

public class Affiliate : BaseEntity
{
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public int TotalReferrals { get; set; } = 0;
    public decimal TotalEarnings { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public TelegramUser User { get; set; } = null!;
    public ICollection<AffiliateReferral> Referrals { get; set; } = new List<AffiliateReferral>();
}
