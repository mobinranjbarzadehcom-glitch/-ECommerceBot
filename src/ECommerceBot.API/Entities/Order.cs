using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class Order : BaseEntity
{
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }

    public string? ReceiptPhotoFileId { get; set; }
    public string? ReceiptPhotoUniqueId { get; set; }
    public string? AccountDetails { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? CouponId { get; set; }
    public decimal DiscountAmount { get; set; } = 0;

    public TelegramUser User { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public Transaction? Transaction { get; set; }
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
