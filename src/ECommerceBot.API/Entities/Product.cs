using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public int CategoryId { get; set; }
    public int DisplayOrder { get; set; } = 0;

    public Category Category { get; set; } = null!;
    public ICollection<ProductKey> ProductKeys { get; set; } = new List<ProductKey>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
