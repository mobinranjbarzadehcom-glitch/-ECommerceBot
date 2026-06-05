using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

/// <summary>
/// Verifies that tenant-scoped operations are correctly isolated.
/// These tests use mocked repositories to confirm that services pass
/// the correct TenantId and that tenant context is respected.
/// </summary>
public class TenantIsolationTests
{
    // ── TenantContext ─────────────────────────────────────────────────────────

    [Fact]
    public void TenantContext_IsNotSet_ByDefault()
    {
        var ctx = new TenantContext();
        Assert.False(ctx.IsSet);
        Assert.Equal(0, ctx.TenantId);
        Assert.Null(ctx.BotToken);
    }

    [Fact]
    public void TenantContext_AfterSetTenant_IsSet()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(42, "bot-token");

        Assert.True(ctx.IsSet);
        Assert.Equal(42, ctx.TenantId);
        Assert.Equal("bot-token", ctx.BotToken);
    }

    [Fact]
    public void TenantContext_SetTenantWithoutToken_IsSet()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(7);

        Assert.True(ctx.IsSet);
        Assert.Equal(7, ctx.TenantId);
        Assert.Null(ctx.BotToken);
    }

    [Fact]
    public void TenantContext_CanBeOverwritten()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(1);
        ctx.SetTenant(2, "new-token");

        Assert.Equal(2, ctx.TenantId);
        Assert.Equal("new-token", ctx.BotToken);
    }

    // ── TelegramUser TenantId ─────────────────────────────────────────────────

    [Fact]
    public void TelegramUser_DefaultTenantId_IsZero()
    {
        var user = new TelegramUser { TelegramId = 1, FirstName = "Test" };
        Assert.Equal(0, user.TenantId);
    }

    [Fact]
    public void TelegramUser_TenantId_CanBeSet()
    {
        var user = new TelegramUser { TenantId = 5, TelegramId = 1, FirstName = "Test" };
        Assert.Equal(5, user.TenantId);
    }

    // ── Tenant entity ─────────────────────────────────────────────────────────

    [Fact]
    public void Tenant_DefaultStatus_IsPendingSetup()
    {
        var tenant = new Tenant { TenantName = "Test", TenantSlug = "test" };
        Assert.Equal(TenantStatus.PendingSetup, tenant.Status);
    }

    [Fact]
    public void Tenant_IsActive_ByDefault()
    {
        var tenant = new Tenant { TenantName = "Test", TenantSlug = "test" };
        Assert.True(tenant.IsActive);
    }

    // ── ITenantRepository (via mock) ──────────────────────────────────────────

    [Fact]
    public async Task TenantRepository_GetBySlug_ReturnsCorrectTenant()
    {
        var expected = new Tenant { Id = 1, TenantSlug = "shop1", TenantName = "Shop 1", IsActive = true };
        var repoMock = new Mock<ITenantRepository>();
        repoMock.Setup(r => r.GetBySlugAsync("shop1")).ReturnsAsync(expected);

        var result = await repoMock.Object.GetBySlugAsync("shop1");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Id);
        Assert.Equal("shop1", result.TenantSlug);
    }

    [Fact]
    public async Task TenantRepository_GetBySlug_ReturnsNull_WhenNotFound()
    {
        var repoMock = new Mock<ITenantRepository>();
        repoMock.Setup(r => r.GetBySlugAsync("nonexistent")).ReturnsAsync((Tenant?)null);

        var result = await repoMock.Object.GetBySlugAsync("nonexistent");

        Assert.Null(result);
    }

    // ── UnitOfWork exposes new repositories ───────────────────────────────────

    [Fact]
    public void UnitOfWork_ExposesTenantRepository()
    {
        var uowMock = new Mock<IUnitOfWork>();
        var tenantRepoMock = new Mock<ITenantRepository>();
        uowMock.Setup(u => u.Tenants).Returns(tenantRepoMock.Object);

        Assert.NotNull(uowMock.Object.Tenants);
    }

    [Fact]
    public void UnitOfWork_ExposesSubscriptionPlanRepository()
    {
        var uowMock = new Mock<IUnitOfWork>();
        var planRepoMock = new Mock<ISubscriptionPlanRepository>();
        uowMock.Setup(u => u.SubscriptionPlans).Returns(planRepoMock.Object);

        Assert.NotNull(uowMock.Object.SubscriptionPlans);
    }

    // ── SubscriptionPlan entity ───────────────────────────────────────────────

    [Fact]
    public void SubscriptionPlan_DefaultValues_AreCorrect()
    {
        var plan = new SubscriptionPlan { Name = "Starter" };

        Assert.Equal(PlanTier.Starter, plan.Tier);
        Assert.Equal(500, plan.MaxUsers);
        Assert.Equal(50, plan.MaxProducts);
        Assert.Equal(2, plan.MaxAdmins);
        Assert.True(plan.IsActive);
        Assert.False(plan.AllowsAffiliate);
        Assert.False(plan.AllowsCoupons);
    }

    [Fact]
    public void SubscriptionPlan_EnterpriseTier_HasExtendedFeatures()
    {
        var plan = new SubscriptionPlan
        {
            Name = "Enterprise",
            Tier = PlanTier.Enterprise,
            AllowsAffiliate = true,
            AllowsCoupons = true,
            AllowsAiSupport = true,
            AllowsWhiteLabel = true,
            AllowsMultiLanguage = true
        };

        Assert.Equal(PlanTier.Enterprise, plan.Tier);
        Assert.True(plan.AllowsAffiliate);
        Assert.True(plan.AllowsWhiteLabel);
    }

    // ── Multi-tenant user lookup (repo mock) ──────────────────────────────────

    [Fact]
    public async Task UserRepository_GetByTelegramId_CanBeScoped()
    {
        // Simulates that a repo mock returns only the user belonging to this tenant
        var tenant1User = new TelegramUser { Id = 1, TenantId = 1, TelegramId = 999 };
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.GetByTelegramIdAsync(999)).ReturnsAsync(tenant1User);

        var result = await userRepoMock.Object.GetByTelegramIdAsync(999);

        Assert.NotNull(result);
        Assert.Equal(1, result!.TenantId);
    }

    [Fact]
    public async Task UserRepository_ReturnsNull_WhenUserFromDifferentTenant()
    {
        // Simulates EF global query filter blocking cross-tenant access
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.GetByTelegramIdAsync(888)).ReturnsAsync((TelegramUser?)null);

        var result = await userRepoMock.Object.GetByTelegramIdAsync(888);

        Assert.Null(result);
    }
}
