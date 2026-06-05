using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IOrderService
{
    Task<ServiceResult<OrderDto>> CreateOrderAsync(int userId, CreateOrderRequest request);
    Task<ServiceResult<OrderDto>> GetOrderByIdAsync(int orderId);
    Task<ServiceResult<IEnumerable<OrderDto>>> GetUserOrdersAsync(int userId);
    Task<ServiceResult<PagedResultDto<OrderDto>>> GetPendingOrdersAsync(int page, int pageSize);
    Task<ServiceResult> ApproveOrderAsync(int orderId, int adminId);
    Task<ServiceResult> RejectOrderAsync(int orderId, int adminId, string reason);
    Task<ServiceResult> ExpireOrderAsync(int orderId);
    Task<int> ExpireStaleOrdersAsync();
}
