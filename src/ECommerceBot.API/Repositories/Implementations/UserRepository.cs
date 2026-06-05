using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class UserRepository : GenericRepository<TelegramUser>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<TelegramUser?> GetByTelegramIdAsync(long telegramId) =>
        await _dbSet.SingleOrDefaultAsync(u => u.TelegramId == telegramId);

    public async Task<TelegramUser?> GetByChatIdAsync(long chatId) =>
        await _dbSet.SingleOrDefaultAsync(u => u.ChatId == chatId);

    public async Task<TelegramUser?> GetWithOrdersAsync(int userId) =>
        await _dbSet
            .Include(u => u.Orders)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .SingleOrDefaultAsync(u => u.Id == userId);

    public async Task<TelegramUser?> GetWithCartAsync(int userId) =>
        await _dbSet
            .Include(u => u.Cart)
                .ThenInclude(c => c!.CartItems)
                    .ThenInclude(ci => ci.Product)
            .SingleOrDefaultAsync(u => u.Id == userId);

    public async Task<IEnumerable<TelegramUser>> GetBlockedUsersAsync() =>
        await _dbSet.Where(u => u.IsBlocked).ToListAsync();

    public async Task<IEnumerable<TelegramUser>> GetAdminsAsync() =>
        await _dbSet.Where(u => u.Role == UserRole.Admin && !u.IsBlocked).ToListAsync();

    public async Task<IEnumerable<TelegramUser>> GetUsersWithStateAsync(ConversationState state) =>
        await _dbSet.Where(u => u.CurrentState == state).ToListAsync();
}
