using ECommerceBot.API.Enums;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class ResourceUsageService : IResourceUsageService
{
    private readonly IUnitOfWork _uow;

    public ResourceUsageService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ResourceUsageSnapshot?> GetSnapshotAsync(int tenantId)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) return null;

        var plan = tenant.PlanId.HasValue
            ? await _uow.SubscriptionPlans.GetByIdAsync(tenant.PlanId.Value)
            : null;

        var userCount = await _uow.Users.CountAsync(u => u.TenantId == tenantId && !u.IsBlocked);
        var productCount = await _uow.Products.CountAsync(p => p.TenantId == tenantId);
        var adminCount = await _uow.Users.CountAsync(
            u => u.TenantId == tenantId && u.Role == UserRole.Admin);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ordersThisMonth = await _uow.Orders.CountAsync(
            o => o.TenantId == tenantId && o.CreatedAt >= monthStart);

        return new ResourceUsageSnapshot(
            PlanName: plan?.Name ?? "بدون پلن",
            UserCount: userCount,
            MaxUsers: tenant.MaxUsers,
            ProductCount: productCount,
            MaxProducts: tenant.MaxProducts,
            AdminCount: adminCount,
            MaxAdmins: tenant.MaxAdmins,
            OrdersThisMonth: ordersThisMonth,
            MaxOrdersPerMonth: tenant.MaxOrdersPerMonth,
            ExpiresAt: tenant.ExpiresAt,
            IsTrial: tenant.IsTrial,
            AllowsAffiliate: plan?.AllowsAffiliate ?? false,
            AllowsCoupons: plan?.AllowsCoupons ?? false,
            AllowsAiSupport: plan?.AllowsAiSupport ?? false,
            AllowsWhiteLabel: plan?.AllowsWhiteLabel ?? false,
            AllowsMultiLanguage: plan?.AllowsMultiLanguage ?? false);
    }
}
