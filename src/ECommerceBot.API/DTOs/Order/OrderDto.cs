using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.Order;

public class OrderDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
    public string? ReceiptPhotoFileId { get; set; }
    public string? AccountDetails { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
}
