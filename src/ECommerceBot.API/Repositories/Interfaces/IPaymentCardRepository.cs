using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IPaymentCardRepository : IGenericRepository<PaymentCard>
{
    Task<IEnumerable<PaymentCard>> GetActiveCardsAsync();
    Task<PaymentCard?> GetDefaultCardAsync();
    Task<PaymentCard?> GetNextRotationCardAsync(int afterCardId);
}
