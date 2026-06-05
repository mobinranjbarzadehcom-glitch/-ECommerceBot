using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class CartRepository : GenericRepository<Cart>, ICartRepository
{
    public CartRepository(AppDbContext context) : base(context) { }

    public async Task<Cart?> GetCartWithItemsAsync(int userId) =>
        await _dbSet
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Product)
            .SingleOrDefaultAsync(c => c.UserId == userId);

    public async Task<CartItem?> GetCartItemAsync(int cartId, int productId) =>
        await _context.CartItems
            .SingleOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId);
}
