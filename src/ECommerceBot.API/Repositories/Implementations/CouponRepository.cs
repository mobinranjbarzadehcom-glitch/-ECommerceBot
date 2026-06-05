using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class CouponRepository : GenericRepository<Coupon>, ICouponRepository
{
    public CouponRepository(AppDbContext context) : base(context) { }

    public async Task<Coupon?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(c => c.Code == code.ToUpper());

    public async Task<IEnumerable<Coupon>> GetActiveAsync() =>
        await _dbSet.Where(c => c.IsActive).OrderByDescending(c => c.CreatedAt).ToListAsync();

    public async Task<bool> HasUserUsedCouponAsync(int couponId, int userId) =>
        await _context.Set<CouponUsage>()
            .AnyAsync(u => u.CouponId == couponId && u.UserId == userId);

    public async Task<int> GetMonthlyOrderCountAsync(int tenantId, DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        return await _context.Set<Order>()
            .Where(o => o.TenantId == tenantId && o.CreatedAt >= start && o.CreatedAt < end)
            .CountAsync();
    }
}
