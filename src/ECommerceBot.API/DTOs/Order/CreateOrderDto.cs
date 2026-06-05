using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.Order;

public class CreateOrderDto
{
    public int UserId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
}
