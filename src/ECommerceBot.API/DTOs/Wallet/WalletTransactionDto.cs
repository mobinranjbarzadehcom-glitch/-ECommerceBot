using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.Wallet;

public class WalletTransactionDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public WalletTransactionType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Description { get; set; }
    public int? RelatedOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
}
