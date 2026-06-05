using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IWalletTransactionRepository : IGenericRepository<WalletTransaction>
{
    Task<IEnumerable<WalletTransaction>> GetByUserIdAsync(int userId);
    Task<IEnumerable<WalletTransaction>> GetByTypeAsync(WalletTransactionType type);
    Task<IEnumerable<WalletTransaction>> GetByOrderIdAsync(int orderId);
}
