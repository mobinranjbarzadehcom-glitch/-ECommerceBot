using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.DTOs.User;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IAdminService
{
    Task<ServiceResult> ApproveOrderAsync(int orderId, int adminId);
    Task<ServiceResult> RejectOrderAsync(int orderId, int adminId, string reason);
    Task<ServiceResult> BlockUserAsync(long telegramId, int adminId);
    Task<ServiceResult> UnblockUserAsync(long telegramId, int adminId);
    Task<ServiceResult> AddAdminNoteAsync(int orderId, string note);
    Task<ServiceResult<PagedResultDto<OrderDto>>> GetPendingOrdersAsync(int page, int pageSize);
    Task<ServiceResult<PagedResultDto<UserDto>>> GetAllUsersAsync(int page, int pageSize);
}
