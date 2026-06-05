namespace ECommerceBot.API.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();
}
