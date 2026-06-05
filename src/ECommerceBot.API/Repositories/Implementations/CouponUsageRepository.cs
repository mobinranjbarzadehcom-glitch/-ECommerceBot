using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;

namespace ECommerceBot.API.Repositories.Implementations;

public class CouponUsageRepository : GenericRepository<CouponUsage>, ICouponUsageRepository
{
    public CouponUsageRepository(AppDbContext context) : base(context) { }
}
