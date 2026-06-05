using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.DTOs.User;
using ECommerceBot.API.Infrastructure.Audit;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class AdminService : IAdminService
{
    private readonly IOrderService _orderService;
    private readonly IUserService _userService;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;

    public AdminService(
        IOrderService orderService,
        IUserService userService,
        IUnitOfWork uow,
        IAuditLogService audit)
    {
        _orderService = orderService;
        _userService = userService;
        _uow = uow;
        _audit = audit;
    }

    public async Task<ServiceResult> ApproveOrderAsync(int orderId, int adminId)
    {
        var result = await _orderService.ApproveOrderAsync(orderId, adminId);
        if (result.IsSuccess)
            await _audit.LogAsync(adminId, AuditAction.ApproveOrder, "Order", orderId);
        return result;
    }

    public async Task<ServiceResult> RejectOrderAsync(int orderId, int adminId, string reason)
    {
        var result = await _orderService.RejectOrderAsync(orderId, adminId, reason);
        if (result.IsSuccess)
            await _audit.LogAsync(adminId, AuditAction.RejectOrder, "Order", orderId, reason);
        return result;
    }

    public async Task<ServiceResult> BlockUserAsync(long telegramId, int adminId)
    {
        var result = await _userService.BlockUserAsync(telegramId);
        if (result.IsSuccess)
        {
            var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
            await _audit.LogAsync(adminId, AuditAction.BlockUser, "User", user?.Id, $"TelegramId={telegramId}");
        }
        return result;
    }

    public async Task<ServiceResult> UnblockUserAsync(long telegramId, int adminId)
    {
        var result = await _userService.UnblockUserAsync(telegramId);
        if (result.IsSuccess)
        {
            var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
            await _audit.LogAsync(adminId, AuditAction.UnblockUser, "User", user?.Id, $"TelegramId={telegramId}");
        }
        return result;
    }

    public async Task<ServiceResult> AddAdminNoteAsync(int orderId, string note)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null)
            return ServiceResult.Failure("Order not found");

        order.AdminNotes = string.IsNullOrEmpty(order.AdminNotes)
            ? note
            : $"{order.AdminNotes}\n{note}";

        _uow.Orders.Update(order);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<PagedResultDto<OrderDto>>> GetPendingOrdersAsync(int page, int pageSize) =>
        await _orderService.GetPendingOrdersAsync(page, pageSize);

    public async Task<ServiceResult<PagedResultDto<UserDto>>> GetAllUsersAsync(int page, int pageSize) =>
        await _userService.GetAllUsersAsync(page, pageSize);
}
