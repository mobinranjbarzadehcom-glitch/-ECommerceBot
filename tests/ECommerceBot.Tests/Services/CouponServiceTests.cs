using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.UnitOfWork;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class CouponServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ICouponRepository> _couponRepoMock;
    private readonly Mock<ICouponUsageRepository> _couponUsageRepoMock;
    private readonly CouponService _sut;

    public CouponServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _tenantContextMock = new Mock<ITenantContext>();
        _couponRepoMock = new Mock<ICouponRepository>();
        _couponUsageRepoMock = new Mock<ICouponUsageRepository>();

        _uowMock.Setup(u => u.Coupons).Returns(_couponRepoMock.Object);
        _uowMock.Setup(u => u.CouponUsages).Returns(_couponUsageRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        _tenantContextMock.Setup(t => t.IsSet).Returns(false);

        _sut = new CouponService(_uowMock.Object, _tenantContextMock.Object);
    }

    // ── ValidateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_WhenCouponNotFound_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("MISSING")).ReturnsAsync((Coupon?)null);

        var result = await _sut.ValidateAsync("MISSING", userId: 1, orderAmount: 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("نامعتبر", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenCouponInactive_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("OFF10"))
            .ReturnsAsync(new Coupon { Id = 1, Code = "OFF10", IsActive = false, DiscountType = DiscountType.Percentage, DiscountValue = 10 });

        var result = await _sut.ValidateAsync("OFF10", userId: 1, orderAmount: 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("نامعتبر", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenCouponExpired_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("OLD"))
            .ReturnsAsync(new Coupon
            {
                Id = 1, Code = "OLD", IsActive = true,
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                DiscountType = DiscountType.Percentage, DiscountValue = 10
            });

        var result = await _sut.ValidateAsync("OLD", userId: 1, orderAmount: 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("منقضی", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenMaxUsesReached_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("FULL"))
            .ReturnsAsync(new Coupon
            {
                Id = 1, Code = "FULL", IsActive = true,
                MaxUses = 10, UsedCount = 10,
                DiscountType = DiscountType.Percentage, DiscountValue = 10
            });

        var result = await _sut.ValidateAsync("FULL", userId: 1, orderAmount: 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("ظرفیت", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenOrderBelowMinAmount_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("MIN200"))
            .ReturnsAsync(new Coupon
            {
                Id = 1, Code = "MIN200", IsActive = true,
                MinOrderAmount = 200,
                DiscountType = DiscountType.Percentage, DiscountValue = 10
            });

        var result = await _sut.ValidateAsync("MIN200", userId: 1, orderAmount: 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("حداقل", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenUserAlreadyUsed_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("USED"))
            .ReturnsAsync(new Coupon { Id = 5, Code = "USED", IsActive = true, DiscountType = DiscountType.Fixed, DiscountValue = 20 });
        _couponRepoMock.Setup(r => r.HasUserUsedCouponAsync(5, 1)).ReturnsAsync(true);

        var result = await _sut.ValidateAsync("USED", userId: 1, orderAmount: 100);

        Assert.False(result.IsSuccess);
        Assert.Contains("قبلاً", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WithValidPercentageCoupon_ReturnsCorrectDiscount()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("SAVE20"))
            .ReturnsAsync(new Coupon { Id = 10, Code = "SAVE20", IsActive = true, DiscountType = DiscountType.Percentage, DiscountValue = 20 });
        _couponRepoMock.Setup(r => r.HasUserUsedCouponAsync(10, 1)).ReturnsAsync(false);

        var result = await _sut.ValidateAsync("SAVE20", userId: 1, orderAmount: 100);

        Assert.True(result.IsSuccess);
        Assert.Equal(20m, result.Data!.DiscountAmount);
        Assert.Equal("SAVE20", result.Data.Coupon.Code);
    }

    [Fact]
    public async Task ValidateAsync_WithValidFixedCoupon_CapsAtOrderAmount()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("FLAT50"))
            .ReturnsAsync(new Coupon { Id = 11, Code = "FLAT50", IsActive = true, DiscountType = DiscountType.Fixed, DiscountValue = 50 });
        _couponRepoMock.Setup(r => r.HasUserUsedCouponAsync(11, 1)).ReturnsAsync(false);

        // Order is 30, fixed discount is 50 → should cap at 30
        var result = await _sut.ValidateAsync("FLAT50", userId: 1, orderAmount: 30);

        Assert.True(result.IsSuccess);
        Assert.Equal(30m, result.Data!.DiscountAmount);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithDuplicateCode_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("EXISTING"))
            .ReturnsAsync(new Coupon { Id = 1, Code = "EXISTING" });

        var result = await _sut.CreateAsync("EXISTING", DiscountType.Percentage, 10, null, null, null);

        Assert.False(result.IsSuccess);
        Assert.Contains("قبلاً", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAsync_WithCodeTooShort_ReturnsFailure()
    {
        var result = await _sut.CreateAsync("AB", DiscountType.Percentage, 10, null, null, null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidPercentage_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("OVER100")).ReturnsAsync((Coupon?)null);

        var result = await _sut.CreateAsync("OVER100", DiscountType.Percentage, 150, null, null, null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesCoupon()
    {
        _couponRepoMock.Setup(r => r.GetByCodeAsync("NEW25")).ReturnsAsync((Coupon?)null);
        _couponRepoMock.Setup(r => r.AddAsync(It.IsAny<Coupon>())).Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync("new25", DiscountType.Percentage, 25, null, 100, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("NEW25", result.Data!.Code); // normalized to uppercase
        Assert.Equal(25m, result.Data.DiscountValue);
        _couponRepoMock.Verify(r => r.AddAsync(It.IsAny<Coupon>()), Times.Once);
    }

    // ── ToggleActiveAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleActiveAsync_WhenNotFound_ReturnsFailure()
    {
        _couponRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Coupon?)null);

        var result = await _sut.ToggleActiveAsync(999);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ToggleActiveAsync_WhenActive_DeactivatesCoupon()
    {
        var coupon = new Coupon { Id = 1, IsActive = true };
        _couponRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(coupon);

        var result = await _sut.ToggleActiveAsync(1);

        Assert.True(result.IsSuccess);
        Assert.False(coupon.IsActive);
    }
}
