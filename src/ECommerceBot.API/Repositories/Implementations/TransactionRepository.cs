using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(AppDbContext context) : base(context) { }

    public async Task<Transaction?> GetByOrderIdAsync(int orderId) =>
        await _dbSet.SingleOrDefaultAsync(t => t.OrderId == orderId);

    public async Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId) =>
        await _dbSet
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Transaction>> GetByStatusAsync(PaymentStatus status) =>
        await _dbSet.Where(t => t.Status == status).ToListAsync();

    public async Task<Transaction?> GetByPaymentReferenceAsync(string paymentReference) =>
        await _dbSet.SingleOrDefaultAsync(t => t.PaymentReference == paymentReference);
}
