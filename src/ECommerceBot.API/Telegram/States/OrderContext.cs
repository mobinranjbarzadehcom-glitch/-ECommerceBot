namespace ECommerceBot.API.Telegram.States;

public class OrderContext
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string? PlayerId { get; set; }
    public int CategoryId { get; set; }
}
