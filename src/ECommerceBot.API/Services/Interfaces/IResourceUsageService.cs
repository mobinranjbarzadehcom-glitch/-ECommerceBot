namespace ECommerceBot.API.Services.Interfaces;

public record ResourceUsageSnapshot(
    string PlanName,
    int UserCount, int MaxUsers,
    int ProductCount, int MaxProducts,
    int AdminCount, int MaxAdmins,
    int OrdersThisMonth, int MaxOrdersPerMonth,
    DateTime? ExpiresAt,
    bool IsTrial,
    bool AllowsAffiliate,
    bool AllowsCoupons,
    bool AllowsAiSupport,
    bool AllowsWhiteLabel,
    bool AllowsMultiLanguage);

public interface IResourceUsageService
{
    Task<ResourceUsageSnapshot?> GetSnapshotAsync(int tenantId);
}
