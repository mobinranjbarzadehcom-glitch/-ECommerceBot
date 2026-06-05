using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IProductKeyRepository : IGenericRepository<ProductKey>
{
    Task<ProductKey?> GetAvailableKeyForProductAsync(int productId);
    Task<IEnumerable<ProductKey>> GetKeysByProductAsync(int productId);
    Task<IEnumerable<ProductKey>> GetUsedKeysByProductAsync(int productId);
}
