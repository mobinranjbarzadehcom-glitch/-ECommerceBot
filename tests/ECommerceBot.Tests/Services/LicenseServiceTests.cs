using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Licensing;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class LicenseServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILicenseRepository> _licenseRepoMock = new();
    private readonly Mock<ILicenseSignatureValidator> _validatorMock = new();
    private readonly Mock<IServerFingerprintService> _fingerprintMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly LicenseStatusCache _cache = new();
    private readonly LicenseService _sut;

    public LicenseServiceTests()
    {
        _uowMock.Setup(u => u.Licenses).Returns(_licenseRepoMock.Object);
        _uowMock.Setup(u => u.Users).Returns(new Mock<IUserRepository>().Object);
        _fingerprintMock.Setup(f => f.GetFingerprint()).Returns("test-fingerprint-abc");

        var options = Options.Create(new LicenseOptions { Enabled = true });
        _sut = new LicenseService(
            _uowMock.Object,
            _validatorMock.Object,
            _fingerprintMock.Object,
            _botClientMock.Object,
            options,
            _cache,
            new Mock<ILogger<LicenseService>>().Object);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoLicense_ReturnsNotActivated()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync((LicenseInfo?)null);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.NotActivated, result.Status);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDisabledLicense_ReturnsDisabled()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = false,
            LicenseKey = "TEST",
            OwnerName = "Test",
            Signature = "sig"
        });

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_WhenSignatureInvalid_ReturnsSignatureInvalid()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "TEST",
            OwnerName = "Test",
            Signature = "bad-signature"
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(false);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.SignatureInvalid, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpired_ReturnsExpired()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "TEST",
            OwnerName = "Test",
            Signature = "sig",
            ExpiresAt = DateTime.UtcNow.AddDays(-10),
            GracePeriodEndsAt = null
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.Expired, result.Status);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenExpiredButInGracePeriod_ReturnsGracePeriod()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "TEST",
            OwnerName = "GraceOwner",
            Edition = "Standard",
            Signature = "sig",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            GracePeriodEndsAt = DateTime.UtcNow.AddDays(2),
            IsTrial = false
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.GracePeriod, result.Status);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ValidLicense_ReturnsValid()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "VALID-KEY",
            OwnerName = "Happy Customer",
            CustomerName = "Corp Inc",
            Edition = "Professional",
            Signature = "sig",
            ExpiresAt = DateTime.UtcNow.AddDays(365),
            IsTrial = false
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.True(result.IsValid);
        Assert.Equal("Happy Customer", result.OwnerName);
        Assert.Equal("Professional", result.Edition);
    }

    [Fact]
    public async Task ValidateAsync_TrialLicense_ReturnsTrial()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "TRIAL",
            OwnerName = "Trial User",
            Edition = "Trial",
            Signature = "sig",
            IsTrial = true,
            ExpiresAt = DateTime.UtcNow.AddDays(14)
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.Trial, result.Status);
        Assert.True(result.IsValid);
        Assert.True(result.IsTrial);
    }

    [Fact]
    public async Task ValidateAsync_ServerMismatch_ReturnsServerMismatch()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "BOUND",
            OwnerName = "Owner",
            Edition = "Standard",
            Signature = "sig",
            ServerFingerprint = "different-fingerprint"
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.ServerMismatch, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_UserLimitExceeded_ReturnsUserLimitExceeded()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "LIMITED",
            OwnerName = "Owner",
            Edition = "Standard",
            Signature = "sig",
            MaxUsers = 10,
            MaxAdmins = 0
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);
        _uowMock.Setup(u => u.Users.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TelegramUser, bool>>>()))
            .ReturnsAsync(11); // over limit

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.UserLimitExceeded, result.Status);
    }

    [Fact]
    public async Task ValidateAsync_AdminLimitExceeded_ReturnsAdminLimitExceeded()
    {
        _licenseRepoMock.Setup(r => r.GetActiveAsync()).ReturnsAsync(new LicenseInfo
        {
            IsActive = true,
            LicenseKey = "ADMINLIMIT",
            OwnerName = "Owner",
            Edition = "Standard",
            Signature = "sig",
            MaxUsers = 0,
            MaxAdmins = 2
        });
        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(true);
        // First call (users): returns 5 (under unlimited), second call (admins): returns 3
        _uowMock.SetupSequence(u => u.Users.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TelegramUser, bool>>>()))
            .ReturnsAsync(3); // admin count over limit

        var result = await _sut.ValidateAsync();

        Assert.Equal(LicenseStatus.AdminLimitExceeded, result.Status);
    }

    [Fact]
    public void GetCachedResult_ReturnsLastResult()
    {
        var expected = LicenseValidationResult.From(LicenseStatus.Valid, "OK");
        _cache.Result = expected;

        var actual = _sut.GetCachedResult();

        Assert.Equal(expected.Status, actual.Status);
    }

    [Fact]
    public async Task ActivateAsync_InvalidBase64_ReturnsInvalid()
    {
        var result = await _sut.ActivateAsync("not-valid-base64!!!");

        Assert.Equal(LicenseStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task ActivateAsync_InvalidSignature_ReturnsSignatureInvalid()
    {
        var package = new
        {
            licenseKey = "TEST-KEY",
            ownerName = "Owner",
            productName = "ECommerceBot",
            edition = "Standard",
            maxUsers = 0,
            maxAdmins = 0,
            isTrial = false,
            issuedAt = DateTime.UtcNow.ToString("O"),
            signature = "bad-sig"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(package);
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        _validatorMock.Setup(v => v.Validate(It.IsAny<LicenseInfo>())).Returns(false);

        var result = await _sut.ActivateAsync(base64);

        Assert.Equal(LicenseStatus.SignatureInvalid, result.Status);
    }
}
