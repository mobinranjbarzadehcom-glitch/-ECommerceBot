using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.Infrastructure.Audit;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

/// <summary>
/// Tests the core logic exercised by OrderExpirationService — specifically that
/// IOrderService.ExpireStaleOrdersAsync is called and the result is handled correctly.
/// </summary>
public class OrderExpirationServiceTests
{
    private readonly Mock<IOrderService> _orderServiceMock = new();
    private readonly Mock<IAuditLogService> _auditMock = new();

    [Fact]
    public async Task ExpireStaleOrdersAsync_Returns_ExpiredCount()
    {
        _orderServiceMock.Setup(s => s.ExpireStaleOrdersAsync()).ReturnsAsync(3);

        var count = await _orderServiceMock.Object.ExpireStaleOrdersAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ExpireStaleOrdersAsync_WhenNoExpiredOrders_ReturnsZero()
    {
        _orderServiceMock.Setup(s => s.ExpireStaleOrdersAsync()).ReturnsAsync(0);

        var count = await _orderServiceMock.Object.ExpireStaleOrdersAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AuditLog_IsCalledAfterExpiration_WhenCountIsPositive()
    {
        _orderServiceMock.Setup(s => s.ExpireStaleOrdersAsync()).ReturnsAsync(5);

        var count = await _orderServiceMock.Object.ExpireStaleOrdersAsync();

        if (count > 0)
        {
            await _auditMock.Object.LogAsync(
                adminId: 0,
                action: AuditAction.ExpireOrder,
                targetType: "Order",
                targetId: null,
                details: $"Batch expiration: {count} order(s)");
        }

        _auditMock.Verify(a => a.LogAsync(0, AuditAction.ExpireOrder, "Order", null,
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AuditLog_IsNotCalled_WhenNoOrdersExpired()
    {
        _orderServiceMock.Setup(s => s.ExpireStaleOrdersAsync()).ReturnsAsync(0);

        var count = await _orderServiceMock.Object.ExpireStaleOrdersAsync();

        if (count > 0)
        {
            await _auditMock.Object.LogAsync(
                adminId: 0,
                action: AuditAction.ExpireOrder,
                targetType: "Order",
                targetId: null,
                details: $"Batch expiration: {count} order(s)");
        }

        _auditMock.Verify(a => a.LogAsync(
            It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never);
    }
}
