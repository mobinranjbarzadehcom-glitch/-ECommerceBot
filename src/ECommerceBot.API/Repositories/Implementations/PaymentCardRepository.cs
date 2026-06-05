using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class PaymentCardRepository : GenericRepository<PaymentCard>, IPaymentCardRepository
{
    public PaymentCardRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<PaymentCard>> GetActiveCardsAsync() =>
        await _dbSet
            .Where(pc => pc.IsActive)
            .OrderBy(pc => pc.DisplayOrder)
            .ToListAsync();

    public async Task<PaymentCard?> GetDefaultCardAsync() =>
        await _dbSet
            .Where(pc => pc.IsActive && pc.IsDefault)
            .FirstOrDefaultAsync();

    public async Task<PaymentCard?> GetNextRotationCardAsync(int afterCardId)
    {
        var cards = await _dbSet
            .Where(pc => pc.IsActive)
            .OrderBy(pc => pc.DisplayOrder)
            .ThenBy(pc => pc.Id)
            .ToListAsync();

        if (cards.Count == 0) return null;

        var currentIndex = cards.FindIndex(c => c.Id == afterCardId);
        var nextIndex = (currentIndex + 1) % cards.Count;
        return cards[nextIndex];
    }
}
