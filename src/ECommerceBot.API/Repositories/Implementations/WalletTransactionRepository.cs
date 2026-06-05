using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class WalletTransactionRepository : GenericRepository<WalletTransaction>, IWalletTransactionRepository
{
    public WalletTransactionRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<WalletTransaction>> GetByUserIdAsync(int userId) =>
        await _dbSet
            .Where(wt => wt.UserId == userId)
            .OrderByDescending(wt => wt.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<WalletTransaction>> GetByTypeAsync(WalletTransactionType type) =>
        await _dbSet
            .Where(wt => wt.Type == type)
            .OrderByDescending(wt => wt.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<WalletTransaction>> GetByOrderIdAsync(int orderId) =>
        await _dbSet
            .Where(wt => wt.RelatedOrderId == orderId)
            .OrderByDescending(wt => wt.CreatedAt)
            .ToListAsync();
}
