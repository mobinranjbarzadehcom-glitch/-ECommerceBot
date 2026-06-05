using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId);
    Task<IEnumerable<Product>> GetByStatusAsync(ProductStatus status);
    Task<Product?> GetWithKeysAsync(int productId);
    Task<IEnumerable<Product>> GetActiveProductsAsync();
    Task<int> GetAvailableKeyCountAsync(int productId);
}
