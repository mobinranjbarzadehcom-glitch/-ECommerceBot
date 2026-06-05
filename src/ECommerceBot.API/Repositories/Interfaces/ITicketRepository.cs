using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ITicketRepository : IGenericRepository<Ticket>
{
    Task<Ticket?> GetTicketWithMessagesAsync(int ticketId);
    Task<IEnumerable<Ticket>> GetByUserIdAsync(int userId);
    Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status);
    Task<IEnumerable<Ticket>> GetOpenTicketsAsync();
}
