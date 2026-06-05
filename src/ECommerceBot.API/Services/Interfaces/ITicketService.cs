using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.Ticket;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface ITicketService
{
    Task<ServiceResult<TicketDto>> CreateTicketAsync(int userId, CreateTicketDto dto);
    Task<ServiceResult> ReplyTicketAsync(int ticketId, int senderId, ReplyTicketDto dto, bool isAdmin);
    Task<ServiceResult> ResolveTicketAsync(int ticketId, int adminId);
    Task<ServiceResult<TicketDto>> GetTicketByIdAsync(int ticketId);
    Task<ServiceResult<IEnumerable<TicketDto>>> GetUserTicketsAsync(int userId);
    Task<ServiceResult<PagedResultDto<TicketDto>>> GetOpenTicketsAsync(int page, int pageSize);
}
