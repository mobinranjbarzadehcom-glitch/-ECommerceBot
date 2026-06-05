using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class ProductKeyRepository : GenericRepository<ProductKey>, IProductKeyRepository
{
    public ProductKeyRepository(AppDbContext context) : base(context) { }

    public async Task<ProductKey?> GetAvailableKeyForProductAsync(int productId) =>
        await _dbSet
            .Where(k => k.ProductId == productId && !k.IsUsed)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<ProductKey>> GetKeysByProductAsync(int productId) =>
        await _dbSet.Where(k => k.ProductId == productId).ToListAsync();

    public async Task<IEnumerable<ProductKey>> GetUsedKeysByProductAsync(int productId) =>
        await _dbSet.Where(k => k.ProductId == productId && k.IsUsed).ToListAsync();
}
