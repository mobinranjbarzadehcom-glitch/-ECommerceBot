using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public async Task<Order?> GetOrderWithItemsAsync(int orderId) =>
        await _dbSet
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.ProductKeys)
            .SingleOrDefaultAsync(o => o.Id == orderId);

    public async Task<IEnumerable<Order>> GetOrdersByUserAsync(int userId) =>
        await _dbSet
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status) =>
        await _dbSet
            .Where(o => o.Status == status)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<Order?> GetOrderWithItemsAndKeysAsync(int orderId) =>
        await _dbSet
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.ProductKeys)
            .Include(o => o.User)
            .SingleOrDefaultAsync(o => o.Id == orderId);

    public async Task<IEnumerable<Order>> GetPendingOrdersByUserAsync(int userId) =>
        await _dbSet
            .Where(o => o.UserId == userId && o.Status == OrderStatus.Pending)
            .ToListAsync();

    public async Task<Order?> GetOrderWithTransactionAsync(int orderId) =>
        await _dbSet
            .Include(o => o.Transaction)
            .SingleOrDefaultAsync(o => o.Id == orderId);

    public async Task<Order?> GetByReceiptUniqueIdAsync(string receiptUniqueId) =>
        await _dbSet
            .SingleOrDefaultAsync(o => o.ReceiptPhotoUniqueId == receiptUniqueId);

    public async Task<IEnumerable<Order>> GetExpiredPendingOrdersAsync() =>
        await _dbSet
            .Where(o => o.Status == OrderStatus.Pending
                     && o.ExpiresAt.HasValue
                     && o.ExpiresAt.Value < DateTime.UtcNow)
            .ToListAsync();
}
