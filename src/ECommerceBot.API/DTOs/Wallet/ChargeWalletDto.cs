namespace ECommerceBot.API.DTOs.Wallet;

public class ChargeWalletDto
{
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
