using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.UnitOfWork;
using Xunit;
using Moq;

namespace ECommerceBot.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IWalletTransactionRepository> _walletRepoMock;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _walletRepoMock = new Mock<IWalletTransactionRepository>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.WalletTransactions).Returns(_walletRepoMock.Object);

        _sut = new UserService(_uowMock.Object);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenUserExists_ReturnsExistingUser()
    {
        var existing = new TelegramUser { Id = 1, TelegramId = 123456, FirstName = "Alice" };
        _userRepoMock.Setup(r => r.GetByTelegramIdAsync(123456)).ReturnsAsync(existing);

        var result = await _sut.GetOrCreateUserAsync(123456, "Alice", null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Id);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<TelegramUser>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenUserNotExists_CreatesNewUser()
    {
        _userRepoMock.Setup(r => r.GetByTelegramIdAsync(999)).ReturnsAsync((TelegramUser?)null);
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<TelegramUser>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.GetOrCreateUserAsync(999, "Bob", "Smith", "bobsmith");

        Assert.True(result.IsSuccess);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<TelegramUser>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ChargeWalletAsync_WithNegativeAmount_ReturnsFailure()
    {
        var result = await _sut.ChargeWalletAsync(1, -100, "Test");

        Assert.False(result.IsSuccess);
        Assert.Contains("positive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChargeWalletAsync_WithValidAmount_UpdatesBalance()
    {
        var user = new TelegramUser { Id = 1, WalletBalance = 50 };
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _uowMock.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.CommitTransactionAsync()).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _walletRepoMock.Setup(r => r.AddAsync(It.IsAny<WalletTransaction>())).Returns(Task.CompletedTask);

        var result = await _sut.ChargeWalletAsync(1, 100, "Top up");

        Assert.True(result.IsSuccess);
        Assert.Equal(150, user.WalletBalance);
    }

    [Fact]
    public async Task AdjustWalletAsync_WhenResultsInNegative_ReturnsFailure()
    {
        var user = new TelegramUser { Id = 1, WalletBalance = 50 };
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var result = await _sut.AdjustWalletAsync(1, -100, "Deduction");

        Assert.False(result.IsSuccess);
        Assert.Contains("negative", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlockUserAsync_WhenUserExists_SetsIsBlockedTrue()
    {
        var user = new TelegramUser { Id = 1, TelegramId = 123, IsBlocked = false };
        _userRepoMock.Setup(r => r.GetByTelegramIdAsync(123)).ReturnsAsync(user);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _sut.BlockUserAsync(123);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsBlocked);
    }

    [Fact]
    public async Task BlockUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        _userRepoMock.Setup(r => r.GetByTelegramIdAsync(999)).ReturnsAsync((TelegramUser?)null);

        var result = await _sut.BlockUserAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found", result.ErrorMessage);
    }
}
