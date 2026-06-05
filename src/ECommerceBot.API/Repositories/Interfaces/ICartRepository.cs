using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ICartRepository : IGenericRepository<Cart>
{
    Task<Cart?> GetCartWithItemsAsync(int userId);
    Task<CartItem?> GetCartItemAsync(int cartId, int productId);
}
