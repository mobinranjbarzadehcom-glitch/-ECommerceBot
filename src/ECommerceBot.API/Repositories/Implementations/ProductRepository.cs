using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId) =>
        await _dbSet.Where(p => p.CategoryId == categoryId && p.Status == ProductStatus.Active)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id).ToListAsync();

    public async Task<IEnumerable<Product>> GetByStatusAsync(ProductStatus status) =>
        await _dbSet.Where(p => p.Status == status).ToListAsync();

    public async Task<Product?> GetWithKeysAsync(int productId) =>
        await _dbSet
            .Include(p => p.ProductKeys)
            .SingleOrDefaultAsync(p => p.Id == productId);

    public async Task<IEnumerable<Product>> GetActiveProductsAsync() =>
        await _dbSet
            .Where(p => p.Status == ProductStatus.Active)
            .Include(p => p.Category)
            .ToListAsync();

    public async Task<int> GetAvailableKeyCountAsync(int productId) =>
        await _context.ProductKeys
            .CountAsync(k => k.ProductId == productId && !k.IsUsed);
}
