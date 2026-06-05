using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class OrderService : IOrderService
{
    private const int MaxPendingOrdersPerUser = 2;
    private const int OrderExpiryHours = 24;

    private readonly IUnitOfWork _uow;
    private readonly IUserService _userService;
    private readonly ITenantContext _tenantContext;

    public OrderService(IUnitOfWork uow, IUserService userService, ITenantContext tenantContext)
    {
        _uow = uow;
        _userService = userService;
        _tenantContext = tenantContext;
    }

    public async Task<ServiceResult<OrderDto>> CreateOrderAsync(int userId, CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountDetails))
            return ServiceResult<OrderDto>.Failure("Account details (Player ID) are required");

        // Enforce MaxOrdersPerMonth plan limit
        if (_tenantContext.IsSet)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(_tenantContext.TenantId);
            if (tenant is not null)
            {
                var monthlyCount = await _uow.Coupons.GetMonthlyOrderCountAsync(_tenantContext.TenantId, DateTime.UtcNow);
                if (monthlyCount >= tenant.MaxOrdersPerMonth)
                    return ServiceResult<OrderDto>.Failure("سقف سفارشات ماهانه این فروشگاه تکمیل شده است.");
            }
        }

        var pendingOrders = await _uow.Orders.GetPendingOrdersByUserAsync(userId);
        if (pendingOrders.Count() >= MaxPendingOrdersPerUser)
            return ServiceResult<OrderDto>.Failure($"You cannot have more than {MaxPendingOrdersPerUser} pending orders at a time");

        if (!string.IsNullOrEmpty(request.ReceiptPhotoUniqueId))
        {
            var duplicate = await _uow.Orders.GetByReceiptUniqueIdAsync(request.ReceiptPhotoUniqueId);
            if (duplicate is not null)
                return ServiceResult<OrderDto>.Failure("This receipt has already been submitted");
        }

        var product = await _uow.Products.GetByIdAsync(request.ProductId);
        if (product is null)
            return ServiceResult<OrderDto>.Failure("Product not found");
        if (product.Status != ProductStatus.Active)
            return ServiceResult<OrderDto>.Failure("Product is not available");

        var total = Math.Max(0, product.Price * request.Quantity - request.DiscountAmount);

        await _uow.BeginTransactionAsync();
        try
        {
            return request.PaymentMethod == PaymentMethod.WalletBalance
                ? await CreateWalletOrderAsync(userId, request, product, total)
                : await CreateReceiptOrderAsync(userId, request, product, total);
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task<ServiceResult<OrderDto>> CreateWalletOrderAsync(
        int userId, CreateOrderRequest request, Product product, decimal total)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
        {
            await _uow.RollbackTransactionAsync();
            return ServiceResult<OrderDto>.Failure("User not found");
        }

        if (user.WalletBalance < total)
        {
            await _uow.RollbackTransactionAsync();
            return ServiceResult<OrderDto>.Failure($"Insufficient wallet balance. Required: {total:F2}, Available: {user.WalletBalance:F2}");
        }

        var availableKeys = (await _uow.ProductKeys.FindAsync(k => k.ProductId == product.Id && !k.IsUsed)).ToList();
        if (availableKeys.Count < request.Quantity)
        {
            await _uow.RollbackTransactionAsync();
            return ServiceResult<OrderDto>.Failure($"Insufficient product keys. Available: {availableKeys.Count}, Required: {request.Quantity}");
        }

        // Create order with items in one shot so EF Core cascade-inserts them
        var order = new Order
        {
            UserId = userId,
            TotalAmount = total,
            DiscountAmount = request.DiscountAmount,
            CouponId = request.CouponId,
            Status = OrderStatus.Completed,
            AccountDetails = request.AccountDetails,
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = request.Quantity,
                    UnitPrice = product.Price
                }
            }
        };
        await _uow.Orders.AddAsync(order);
        await _uow.SaveChangesAsync();

        // After SaveChanges, IDs are populated
        var orderItem = order.OrderItems.First();
        var keysToAssign = availableKeys.Take(request.Quantity).ToList();
        foreach (var key in keysToAssign)
        {
            key.IsUsed = true;
            key.OrderItemId = orderItem.Id;
            _uow.ProductKeys.Update(key);
        }

        var balanceBefore = user.WalletBalance;
        user.WalletBalance -= total;
        _uow.Users.Update(user);

        await _uow.WalletTransactions.AddAsync(new WalletTransaction
        {
            UserId = userId,
            Amount = -total,
            BalanceBefore = balanceBefore,
            BalanceAfter = user.WalletBalance,
            Type = WalletTransactionType.Purchase,
            Description = $"Purchase: {product.Name}",
            RelatedOrderId = order.Id
        });

        await _uow.Transactions.AddAsync(new Transaction
        {
            OrderId = order.Id,
            UserId = userId,
            Amount = total,
            Status = PaymentStatus.Completed,
            Method = PaymentMethod.WalletBalance,
            PaidAt = DateTime.UtcNow
        });

        await _uow.SaveChangesAsync();
        await _uow.CommitTransactionAsync();

        var result = await _uow.Orders.GetOrderWithItemsAndKeysAsync(order.Id);
        return ServiceResult<OrderDto>.Success(MapToDto(result!, user.FirstName));
    }

    private async Task<ServiceResult<OrderDto>> CreateReceiptOrderAsync(
        int userId, CreateOrderRequest request, Product product, decimal total)
    {
        if (string.IsNullOrWhiteSpace(request.ReceiptPhotoFileId) || string.IsNullOrWhiteSpace(request.ReceiptPhotoUniqueId))
        {
            await _uow.RollbackTransactionAsync();
            return ServiceResult<OrderDto>.Failure("Receipt photo is required for this payment method");
        }

        var order = new Order
        {
            UserId = userId,
            TotalAmount = total,
            DiscountAmount = request.DiscountAmount,
            CouponId = request.CouponId,
            Status = OrderStatus.Pending,
            ReceiptPhotoFileId = request.ReceiptPhotoFileId,
            ReceiptPhotoUniqueId = request.ReceiptPhotoUniqueId,
            AccountDetails = request.AccountDetails,
            ExpiresAt = DateTime.UtcNow.AddHours(OrderExpiryHours),
            OrderItems = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = request.Quantity,
                    UnitPrice = product.Price
                }
            }
        };
        await _uow.Orders.AddAsync(order);
        await _uow.SaveChangesAsync();

        await _uow.Transactions.AddAsync(new Transaction
        {
            OrderId = order.Id,
            UserId = userId,
            Amount = total,
            Status = PaymentStatus.Pending,
            Method = request.PaymentMethod,
            PaymentReference = request.ReceiptPhotoUniqueId
        });
        await _uow.SaveChangesAsync();
        await _uow.CommitTransactionAsync();

        var user = await _uow.Users.GetByIdAsync(userId);
        var result = await _uow.Orders.GetOrderWithItemsAsync(order.Id);
        return ServiceResult<OrderDto>.Success(MapToDto(result!, user?.FirstName ?? string.Empty));
    }

    public async Task<ServiceResult<OrderDto>> GetOrderByIdAsync(int orderId)
    {
        var order = await _uow.Orders.GetOrderWithItemsAndKeysAsync(orderId);
        if (order is null)
            return ServiceResult<OrderDto>.Failure("Order not found");

        return ServiceResult<OrderDto>.Success(MapToDto(order, order.User?.FirstName ?? string.Empty));
    }

    public async Task<ServiceResult<IEnumerable<OrderDto>>> GetUserOrdersAsync(int userId)
    {
        var orders = await _uow.Orders.GetOrdersByUserAsync(userId);
        var user = await _uow.Users.GetByIdAsync(userId);
        var name = user?.FirstName ?? string.Empty;
        return ServiceResult<IEnumerable<OrderDto>>.Success(orders.Select(o => MapToDto(o, name)));
    }

    public async Task<ServiceResult<PagedResultDto<OrderDto>>> GetPendingOrdersAsync(int page, int pageSize)
    {
        var all = (await _uow.Orders.GetOrdersByStatusAsync(OrderStatus.Pending)).ToList();
        var totalCount = all.Count;
        var items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => MapToDto(o, o.User?.FirstName ?? string.Empty))
            .ToList();

        return ServiceResult<PagedResultDto<OrderDto>>.Success(new PagedResultDto<OrderDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult> ApproveOrderAsync(int orderId, int adminId)
    {
        var order = await _uow.Orders.GetOrderWithItemsAndKeysAsync(orderId);
        if (order is null)
            return ServiceResult.Failure("Order not found");

        if (order.Status != OrderStatus.Pending)
            return ServiceResult.Failure($"Order is not pending (current status: {order.Status})");

        // Pre-validate key availability
        foreach (var item in order.OrderItems)
        {
            var count = await _uow.ProductKeys.CountAsync(k => k.ProductId == item.ProductId && !k.IsUsed);
            if (count < item.Quantity)
                return ServiceResult.Failure($"Insufficient product keys for product ID {item.ProductId}. Available: {count}, Required: {item.Quantity}");
        }

        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var item in order.OrderItems)
            {
                var keys = (await _uow.ProductKeys.FindAsync(k => k.ProductId == item.ProductId && !k.IsUsed))
                    .Take(item.Quantity)
                    .ToList();

                foreach (var key in keys)
                {
                    key.IsUsed = true;
                    key.OrderItemId = item.Id;
                    _uow.ProductKeys.Update(key);
                }
            }

            order.Status = OrderStatus.Completed;
            order.AdminNotes = $"Approved by admin ID {adminId} at {DateTime.UtcNow:u}";
            _uow.Orders.Update(order);

            var orderWithTx = await _uow.Orders.GetOrderWithTransactionAsync(orderId);
            if (orderWithTx?.Transaction is not null)
            {
                orderWithTx.Transaction.Status = PaymentStatus.Completed;
                orderWithTx.Transaction.PaidAt = DateTime.UtcNow;
                _uow.Transactions.Update(orderWithTx.Transaction);
            }

            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
            return ServiceResult.Success();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ServiceResult> RejectOrderAsync(int orderId, int adminId, string reason)
    {
        var order = await _uow.Orders.GetOrderWithTransactionAsync(orderId);
        if (order is null)
            return ServiceResult.Failure("Order not found");

        if (order.Status != OrderStatus.Pending)
            return ServiceResult.Failure($"Order is not pending (current status: {order.Status})");

        await _uow.BeginTransactionAsync();
        try
        {
            order.Status = OrderStatus.Cancelled;
            order.AdminNotes = reason;
            _uow.Orders.Update(order);

            if (order.Transaction is not null)
            {
                order.Transaction.Status = PaymentStatus.Failed;
                order.Transaction.FailureReason = reason;
                _uow.Transactions.Update(order.Transaction);

                if (order.Transaction.Method == PaymentMethod.WalletBalance)
                {
                    await _uow.SaveChangesAsync();
                    var refundResult = await _userService.RefundToWalletAsync(
                        order.UserId, order.TotalAmount, orderId,
                        $"Order #{orderId} rejected: {reason}");

                    if (!refundResult.IsSuccess)
                    {
                        await _uow.RollbackTransactionAsync();
                        return ServiceResult.Failure($"Failed to refund wallet: {refundResult.ErrorMessage}");
                    }
                    await _uow.CommitTransactionAsync();
                    return ServiceResult.Success();
                }
            }

            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
            return ServiceResult.Success();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ServiceResult> ExpireOrderAsync(int orderId)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null)
            return ServiceResult.Failure("Order not found");

        if (order.Status != OrderStatus.Pending)
            return ServiceResult.Failure("Order is not pending");

        return await RejectOrderAsync(orderId, 0, "Order expired due to no payment confirmation");
    }

    public async Task<int> ExpireStaleOrdersAsync()
    {
        var expiredOrders = await _uow.Orders.GetExpiredPendingOrdersAsync();
        var count = 0;
        foreach (var order in expiredOrders)
        {
            var result = await ExpireOrderAsync(order.Id);
            if (result.IsSuccess) count++;
        }
        return count;
    }

    private static OrderDto MapToDto(Order o, string userName) => new()
    {
        Id = o.Id,
        UserId = o.UserId,
        UserName = userName,
        TotalAmount = o.TotalAmount,
        Status = o.Status,
        Notes = o.Notes,
        ReceiptPhotoFileId = o.ReceiptPhotoFileId,
        AccountDetails = o.AccountDetails,
        AdminNotes = o.AdminNotes,
        ExpiresAt = o.ExpiresAt,
        CreatedAt = o.CreatedAt,
        Items = o.OrderItems.Select(oi => new OrderItemDto
        {
            Id = oi.Id,
            ProductId = oi.ProductId,
            ProductName = oi.Product?.Name ?? string.Empty,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice,
            Keys = oi.ProductKeys.Select(k => k.KeyValue).ToList()
        }).ToList()
    };
}
