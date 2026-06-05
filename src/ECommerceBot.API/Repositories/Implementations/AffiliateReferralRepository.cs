using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;

namespace ECommerceBot.API.Repositories.Implementations;

public class AffiliateReferralRepository : GenericRepository<AffiliateReferral>, IAffiliateReferralRepository
{
    public AffiliateReferralRepository(AppDbContext context) : base(context) { }
}
