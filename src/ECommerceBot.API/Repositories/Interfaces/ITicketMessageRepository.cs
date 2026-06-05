using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ITicketMessageRepository : IGenericRepository<TicketMessage>
{
    Task<IEnumerable<TicketMessage>> GetByTicketIdAsync(int ticketId);
}
