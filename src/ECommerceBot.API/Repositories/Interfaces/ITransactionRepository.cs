using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ITransactionRepository : IGenericRepository<Transaction>
{
    Task<Transaction?> GetByOrderIdAsync(int orderId);
    Task<IEnumerable<Transaction>> GetByUserIdAsync(int userId);
    Task<IEnumerable<Transaction>> GetByStatusAsync(PaymentStatus status);
    Task<Transaction?> GetByPaymentReferenceAsync(string paymentReference);
}
