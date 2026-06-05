namespace ECommerceBot.API.Entities;

public class Cart : BaseEntity
{
    public int UserId { get; set; }

    public TelegramUser User { get; set; } = null!;
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
