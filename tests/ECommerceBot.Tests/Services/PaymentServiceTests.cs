using ECommerceBot.API.DTOs.Payment;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Xunit;
using Moq;

namespace ECommerceBot.Tests.Services;

public class PaymentServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ISettingService> _settingServiceMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _settingServiceMock = new Mock<ISettingService>();
        _orderRepoMock = new Mock<IOrderRepository>();

        _uowMock.Setup(u => u.Orders).Returns(_orderRepoMock.Object);

        _sut = new PaymentService(_uowMock.Object, _settingServiceMock.Object);
    }

    [Fact]
    public async Task ValidateReceiptUniqueIdAsync_WithEmptyId_ReturnsFailure()
    {
        var result = await _sut.ValidateReceiptUniqueIdAsync(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateReceiptUniqueIdAsync_WhenDuplicate_ReturnsFailure()
    {
        _orderRepoMock.Setup(r => r.GetByReceiptUniqueIdAsync("uid_dup")).ReturnsAsync(new Order());

        var result = await _sut.ValidateReceiptUniqueIdAsync("uid_dup");

        Assert.False(result.IsSuccess);
        Assert.Contains("already been used", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateReceiptUniqueIdAsync_WhenUnique_ReturnsSuccess()
    {
        _orderRepoMock.Setup(r => r.GetByReceiptUniqueIdAsync("uid_new")).ReturnsAsync((Order?)null);

        var result = await _sut.ValidateReceiptUniqueIdAsync("uid_new");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetActivePaymentCardAsync_DelegatesTo_SettingService()
    {
        var card = new PaymentCardDto { Id = 1, CardNumber = "1234-5678" };
        _settingServiceMock.Setup(s => s.GetActivePaymentCardAsync())
            .ReturnsAsync(ServiceResult<PaymentCardDto>.Success(card));

        var result = await _sut.GetActivePaymentCardAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("1234-5678", result.Data!.CardNumber);
    }

    [Fact]
    public async Task SubmitReceiptAsync_WithEmptyFileId_ReturnsFailure()
    {
        var result = await _sut.SubmitReceiptAsync(1, string.Empty, "uid123");

        Assert.False(result.IsSuccess);
        Assert.Contains("file ID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitReceiptAsync_WhenOrderNotFound_ReturnsFailure()
    {
        _orderRepoMock.Setup(r => r.GetByReceiptUniqueIdAsync("uid_new")).ReturnsAsync((Order?)null);
        _orderRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Order?)null);

        var result = await _sut.SubmitReceiptAsync(999, "file123", "uid_new");

        Assert.False(result.IsSuccess);
        Assert.Equal("Order not found", result.ErrorMessage);
    }

    [Fact]
    public async Task SubmitReceiptAsync_WhenOrderNotPending_ReturnsFailure()
    {
        var order = new Order { Id = 1, Status = OrderStatus.Completed };
        _orderRepoMock.Setup(r => r.GetByReceiptUniqueIdAsync("uid_new")).ReturnsAsync((Order?)null);
        _orderRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(order);

        var result = await _sut.SubmitReceiptAsync(1, "file123", "uid_new");

        Assert.False(result.IsSuccess);
        Assert.Contains("pending", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
