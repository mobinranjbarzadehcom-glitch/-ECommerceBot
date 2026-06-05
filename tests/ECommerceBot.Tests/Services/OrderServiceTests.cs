using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IProductRepository> _productRepoMock;
    private readonly Mock<IProductKeyRepository> _productKeyRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ITransactionRepository> _transactionRepoMock;
    private readonly Mock<IWalletTransactionRepository> _walletRepoMock;
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userServiceMock = new Mock<IUserService>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _productRepoMock = new Mock<IProductRepository>();
        _productKeyRepoMock = new Mock<IProductKeyRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _transactionRepoMock = new Mock<ITransactionRepository>();
        _walletRepoMock = new Mock<IWalletTransactionRepository>();

        _uowMock.Setup(u => u.Orders).Returns(_orderRepoMock.Object);
        _uowMock.Setup(u => u.Products).Returns(_productRepoMock.Object);
        _uowMock.Setup(u => u.ProductKeys).Returns(_productKeyRepoMock.Object);
        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.Transactions).Returns(_transactionRepoMock.Object);
        _uowMock.Setup(u => u.WalletTransactions).Returns(_walletRepoMock.Object);

        _sut = new OrderService(_uowMock.Object, _userServiceMock.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_WithoutAccountDetails_ReturnsFailure()
    {
        var request = new CreateOrderRequest { ProductId = 1, Quantity = 1, PaymentMethod = PaymentMethod.WalletBalance };

        var result = await _sut.CreateOrderAsync(1, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Account details", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenTwoPendingOrders_ReturnsAntiSpamFailure()
    {
        var request = new CreateOrderRequest
        {
            ProductId = 1, Quantity = 1,
            PaymentMethod = PaymentMethod.CardPayment,
            AccountDetails = "player123"
        };
        _orderRepoMock.Setup(r => r.GetPendingOrdersByUserAsync(1))
            .ReturnsAsync(new[] { new Order(), new Order() });

        var result = await _sut.CreateOrderAsync(1, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("pending orders", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrderAsync_WithDuplicateReceipt_ReturnsFailure()
    {
        var request = new CreateOrderRequest
        {
            ProductId = 1, Quantity = 1,
            PaymentMethod = PaymentMethod.CardPayment,
            AccountDetails = "player123",
            ReceiptPhotoUniqueId = "uid_existing"
        };
        _orderRepoMock.Setup(r => r.GetPendingOrdersByUserAsync(1)).ReturnsAsync(Array.Empty<Order>());
        _orderRepoMock.Setup(r => r.GetByReceiptUniqueIdAsync("uid_existing")).ReturnsAsync(new Order());

        var result = await _sut.CreateOrderAsync(1, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("receipt", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenProductNotFound_ReturnsFailure()
    {
        var request = new CreateOrderRequest
        {
            ProductId = 999, Quantity = 1,
            PaymentMethod = PaymentMethod.WalletBalance,
            AccountDetails = "player123"
        };
        _orderRepoMock.Setup(r => r.GetPendingOrdersByUserAsync(1)).ReturnsAsync(Array.Empty<Order>());
        _productRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Product?)null);

        var result = await _sut.CreateOrderAsync(1, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Product not found", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrderAsync_WalletPayment_WithInsufficientBalance_ReturnsFailure()
    {
        var request = new CreateOrderRequest
        {
            ProductId = 1, Quantity = 1,
            PaymentMethod = PaymentMethod.WalletBalance,
            AccountDetails = "player123"
        };
        var product = new Product { Id = 1, Name = "Test", Price = 100, Status = ProductStatus.Active };
        var user = new TelegramUser { Id = 1, WalletBalance = 50 };

        _orderRepoMock.Setup(r => r.GetPendingOrdersByUserAsync(1)).ReturnsAsync(Array.Empty<Order>());
        _productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _uowMock.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.RollbackTransactionAsync()).Returns(Task.CompletedTask);

        var result = await _sut.CreateOrderAsync(1, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Insufficient", result.ErrorMessage);
    }

    [Fact]
    public async Task ApproveOrderAsync_WhenOrderNotFound_ReturnsFailure()
    {
        _orderRepoMock.Setup(r => r.GetOrderWithItemsAndKeysAsync(999)).ReturnsAsync((Order?)null);

        var result = await _sut.ApproveOrderAsync(999, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal("Order not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ApproveOrderAsync_WhenOrderNotPending_ReturnsFailure()
    {
        var order = new Order { Id = 1, Status = OrderStatus.Completed, OrderItems = new List<OrderItem>() };
        _orderRepoMock.Setup(r => r.GetOrderWithItemsAndKeysAsync(1)).ReturnsAsync(order);

        var result = await _sut.ApproveOrderAsync(1, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("not pending", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectOrderAsync_WhenOrderNotFound_ReturnsFailure()
    {
        _orderRepoMock.Setup(r => r.GetOrderWithTransactionAsync(999)).ReturnsAsync((Order?)null);

        var result = await _sut.RejectOrderAsync(999, 1, "Test rejection");

        Assert.False(result.IsSuccess);
        Assert.Equal("Order not found", result.ErrorMessage);
    }

    [Fact]
    public async Task GetPendingOrdersAsync_ReturnsPaged()
    {
        var orders = Enumerable.Range(1, 5)
            .Select(i => new Order { Id = i, Status = OrderStatus.Pending, OrderItems = new List<OrderItem>() })
            .ToList();

        _orderRepoMock.Setup(r => r.GetOrdersByStatusAsync(OrderStatus.Pending)).ReturnsAsync(orders);

        var result = await _sut.GetPendingOrdersAsync(1, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Data!.TotalCount);
        Assert.Equal(3, result.Data.Items.Count());
    }
}
