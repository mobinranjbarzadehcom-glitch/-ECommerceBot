using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Category>> GetActiveCategoriesAsync() =>
        await _dbSet.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync();

    public async Task<Category?> GetWithProductsAsync(int categoryId) =>
        await _dbSet
            .Include(c => c.Products)
            .SingleOrDefaultAsync(c => c.Id == categoryId);
}
