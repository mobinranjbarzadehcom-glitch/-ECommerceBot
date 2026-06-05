namespace ECommerceBot.API.Entities;

public class CouponUsage : BaseEntity
{
    public int TenantId { get; set; }
    public int CouponId { get; set; }
    public int UserId { get; set; }
    public int? OrderId { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;

    public Coupon Coupon { get; set; } = null!;
    public TelegramUser User { get; set; } = null!;
    public Order? Order { get; set; }
}
