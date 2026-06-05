namespace ECommerceBot.API.DTOs.Wallet;

public class AdjustWalletDto
{
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
