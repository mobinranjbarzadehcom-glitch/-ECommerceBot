namespace ECommerceBot.API.DTOs.Payment;

public class PaymentCardDto
{
    public int Id { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
}
