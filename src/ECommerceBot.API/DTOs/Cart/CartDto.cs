namespace ECommerceBot.API.DTOs.Cart;

public class CartDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ICollection<CartItemDto> Items { get; set; } = new List<CartItemDto>();
    public decimal TotalAmount => Items.Sum(i => i.TotalPrice);
}
