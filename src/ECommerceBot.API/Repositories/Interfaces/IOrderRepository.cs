using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetOrderWithItemsAsync(int orderId);
    Task<Order?> GetOrderWithItemsAndKeysAsync(int orderId);
    Task<IEnumerable<Order>> GetOrdersByUserAsync(int userId);
    Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status);
    Task<IEnumerable<Order>> GetPendingOrdersByUserAsync(int userId);
    Task<Order?> GetOrderWithTransactionAsync(int orderId);
    Task<Order?> GetByReceiptUniqueIdAsync(string receiptUniqueId);
    Task<IEnumerable<Order>> GetExpiredPendingOrdersAsync();
}
