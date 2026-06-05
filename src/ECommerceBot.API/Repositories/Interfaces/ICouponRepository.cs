using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ICouponRepository : IGenericRepository<Coupon>
{
    Task<Coupon?> GetByCodeAsync(string code);
    Task<IEnumerable<Coupon>> GetActiveAsync();
    Task<bool> HasUserUsedCouponAsync(int couponId, int userId);
    Task<int> GetMonthlyOrderCountAsync(int tenantId, DateTime month);
}
