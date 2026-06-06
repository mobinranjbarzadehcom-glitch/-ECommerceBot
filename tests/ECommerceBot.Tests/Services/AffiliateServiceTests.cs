using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.UnitOfWork;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class AffiliateServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IAffiliateRepository> _affiliateRepoMock;
    private readonly Mock<IAffiliateReferralRepository> _referralRepoMock;
    private readonly AffiliateService _sut;

    public AffiliateServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userServiceMock = new Mock<IUserService>();
        _affiliateRepoMock = new Mock<IAffiliateRepository>();
        _referralRepoMock = new Mock<IAffiliateReferralRepository>();

        _uowMock.Setup(u => u.Affiliates).Returns(_affiliateRepoMock.Object);
        _uowMock.Setup(u => u.AffiliateReferrals).Returns(_referralRepoMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        _sut = new AffiliateService(_uowMock.Object, _userServiceMock.Object);
    }

    // ── GetOrCreateAffiliateAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateAffiliateAsync_WhenExists_ReturnsExisting()
    {
        var existing = new Affiliate { Id = 1, UserId = 42, ReferralCode = "REF42ABC", IsActive = true };
        _affiliateRepoMock.Setup(r => r.GetByUserIdAsync(42)).ReturnsAsync(existing);

        var result = await _sut.GetOrCreateAffiliateAsync(userId: 42);

        Assert.True(result.IsSuccess);
        Assert.Equal("REF42ABC", result.Data!.ReferralCode);
        _affiliateRepoMock.Verify(r => r.AddAsync(It.IsAny<Affiliate>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateAffiliateAsync_WhenNotExists_CreatesNew()
    {
        _affiliateRepoMock.Setup(r => r.GetByUserIdAsync(99)).ReturnsAsync((Affiliate?)null);
        _affiliateRepoMock.Setup(r => r.AddAsync(It.IsAny<Affiliate>())).Returns(Task.CompletedTask);

        var result = await _sut.GetOrCreateAffiliateAsync(userId: 99);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("REF99", result.Data!.ReferralCode);
        _affiliateRepoMock.Verify(r => r.AddAsync(It.IsAny<Affiliate>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── TrackReferralAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task TrackReferralAsync_WhenUserAlreadyReferred_ReturnsFailure()
    {
        _affiliateRepoMock.Setup(r => r.IsUserReferredAsync(5)).ReturnsAsync(true);

        var result = await _sut.TrackReferralAsync("ANYCODE", newUserId: 5);

        Assert.False(result.IsSuccess);
        Assert.Contains("قبلاً", result.ErrorMessage);
    }

    [Fact]
    public async Task TrackReferralAsync_WhenSelfReferral_ReturnsFailure()
    {
        _affiliateRepoMock.Setup(r => r.IsUserReferredAsync(10)).ReturnsAsync(false);
        _affiliateRepoMock.Setup(r => r.GetByCodeAsync("SELFREF"))
            .ReturnsAsync(new Affiliate { Id = 1, UserId = 10, ReferralCode = "SELFREF" });

        var result = await _sut.TrackReferralAsync("SELFREF", newUserId: 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("خودتان", result.ErrorMessage);
    }

    [Fact]
    public async Task TrackReferralAsync_WhenCodeNotFound_ReturnsFailure()
    {
        _affiliateRepoMock.Setup(r => r.IsUserReferredAsync(20)).ReturnsAsync(false);
        _affiliateRepoMock.Setup(r => r.GetByCodeAsync("BOGUS")).ReturnsAsync((Affiliate?)null);

        var result = await _sut.TrackReferralAsync("BOGUS", newUserId: 20);

        Assert.False(result.IsSuccess);
        Assert.Contains("نامعتبر", result.ErrorMessage);
    }

    [Fact]
    public async Task TrackReferralAsync_WithValidReferral_CreditsReferrerAndSaves()
    {
        var referrer = new Affiliate { Id = 3, UserId = 7, ReferralCode = "VALIDREF", TotalReferrals = 2, TotalEarnings = 10m };
        _affiliateRepoMock.Setup(r => r.IsUserReferredAsync(99)).ReturnsAsync(false);
        _affiliateRepoMock.Setup(r => r.GetByCodeAsync("VALIDREF")).ReturnsAsync(referrer);
        _referralRepoMock.Setup(r => r.AddAsync(It.IsAny<AffiliateReferral>())).Returns(Task.CompletedTask);
        _userServiceMock.Setup(s => s.AddBonusAsync(7, It.IsAny<decimal>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult.Success());

        var result = await _sut.TrackReferralAsync("VALIDREF", newUserId: 99);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, referrer.TotalReferrals);
        Assert.True(referrer.TotalEarnings > 10m);
        _userServiceMock.Verify(s => s.AddBonusAsync(7, It.IsAny<decimal>(), It.IsAny<string>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
