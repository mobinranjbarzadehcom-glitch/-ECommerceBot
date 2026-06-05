using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class Transaction : BaseEntity
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentMethod Method { get; set; }
    public string? PaymentReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? PaidAt { get; set; }

    public Order Order { get; set; } = null!;
    public TelegramUser User { get; set; } = null!;
}
