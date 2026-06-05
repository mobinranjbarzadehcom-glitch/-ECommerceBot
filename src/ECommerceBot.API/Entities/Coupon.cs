using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class Coupon : BaseEntity
{
    public int TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
    public decimal DiscountValue { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; } = 0;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CouponUsage> Usages { get; set; } = new List<CouponUsage>();
}
