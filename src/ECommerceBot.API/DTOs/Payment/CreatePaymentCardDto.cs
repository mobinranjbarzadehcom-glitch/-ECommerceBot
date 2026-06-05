namespace ECommerceBot.API.DTOs.Payment;

public class CreatePaymentCardDto
{
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
}
