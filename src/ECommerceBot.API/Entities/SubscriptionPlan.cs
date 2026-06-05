using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public PlanTier Tier { get; set; } = PlanTier.Starter;
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }
    public int MaxUsers { get; set; } = 500;
    public int MaxProducts { get; set; } = 50;
    public int MaxAdmins { get; set; } = 2;
    public int MaxOrdersPerMonth { get; set; } = 200;
    public bool AllowsAffiliate { get; set; } = false;
    public bool AllowsCoupons { get; set; } = false;
    public bool AllowsAiSupport { get; set; } = false;
    public bool AllowsWhiteLabel { get; set; } = false;
    public bool AllowsMultiLanguage { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
}
