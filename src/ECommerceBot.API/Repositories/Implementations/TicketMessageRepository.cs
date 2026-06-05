using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class TicketMessageRepository : GenericRepository<TicketMessage>, ITicketMessageRepository
{
    public TicketMessageRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<TicketMessage>> GetByTicketIdAsync(int ticketId) =>
        await _dbSet
            .Where(m => m.TicketId == ticketId)
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
}
