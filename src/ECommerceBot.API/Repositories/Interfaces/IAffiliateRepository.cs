using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IAffiliateRepository : IGenericRepository<Affiliate>
{
    Task<Affiliate?> GetByCodeAsync(string code);
    Task<Affiliate?> GetByUserIdAsync(int userId);
    Task<bool> IsUserReferredAsync(int userId);
    Task<AffiliateReferral?> GetReferralByUserAsync(int referredUserId);
}
