using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class TicketRepository : GenericRepository<Ticket>, ITicketRepository
{
    public TicketRepository(AppDbContext context) : base(context) { }

    public async Task<Ticket?> GetTicketWithMessagesAsync(int ticketId) =>
        await _dbSet
            .Include(t => t.User)
            .Include(t => t.AssignedAdmin)
            .Include(t => t.Messages)
                .ThenInclude(m => m.Sender)
            .SingleOrDefaultAsync(t => t.Id == ticketId);

    public async Task<IEnumerable<Ticket>> GetByUserIdAsync(int userId) =>
        await _dbSet
            .Where(t => t.UserId == userId)
            .Include(t => t.Messages)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status) =>
        await _dbSet
            .Where(t => t.Status == status)
            .Include(t => t.User)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Ticket>> GetOpenTicketsAsync() =>
        await _dbSet
            .Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
            .Include(t => t.User)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
}
