using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.Order;

public class CreateOrderRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReceiptPhotoFileId { get; set; }
    public string? ReceiptPhotoUniqueId { get; set; }
    public string? AccountDetails { get; set; }
}
