using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class AffiliateRepository : GenericRepository<Affiliate>, IAffiliateRepository
{
    public AffiliateRepository(AppDbContext context) : base(context) { }

    public async Task<Affiliate?> GetByCodeAsync(string code) =>
        await _dbSet.Include(a => a.User)
            .FirstOrDefaultAsync(a => a.ReferralCode == code.ToUpper() && a.IsActive);

    public async Task<Affiliate?> GetByUserIdAsync(int userId) =>
        await _dbSet.FirstOrDefaultAsync(a => a.UserId == userId);

    public async Task<bool> IsUserReferredAsync(int userId) =>
        await _context.Set<AffiliateReferral>()
            .AnyAsync(r => r.ReferredUserId == userId);

    public async Task<AffiliateReferral?> GetReferralByUserAsync(int referredUserId) =>
        await _context.Set<AffiliateReferral>()
            .Include(r => r.Affiliate)
            .FirstOrDefaultAsync(r => r.ReferredUserId == referredUserId);
}
