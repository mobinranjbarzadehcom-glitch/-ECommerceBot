using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class WalletTransaction : BaseEntity
{
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public WalletTransactionType Type { get; set; }
    public string? Description { get; set; }
    public int? RelatedOrderId { get; set; }

    public TelegramUser User { get; set; } = null!;
    public Order? RelatedOrder { get; set; }
}
