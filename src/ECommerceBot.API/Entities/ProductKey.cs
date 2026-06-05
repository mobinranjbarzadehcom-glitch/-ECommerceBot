namespace ECommerceBot.API.Entities;

public class ProductKey : BaseEntity
{
    public string KeyValue { get; set; } = string.Empty;
    public bool IsUsed { get; set; } = false;
    public int ProductId { get; set; }
    public int? OrderItemId { get; set; }

    public Product Product { get; set; } = null!;
    public OrderItem? OrderItem { get; set; }
}
