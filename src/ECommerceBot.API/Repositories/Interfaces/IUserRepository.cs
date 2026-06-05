using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface IUserRepository : IGenericRepository<TelegramUser>
{
    Task<TelegramUser?> GetByTelegramIdAsync(long telegramId);
    Task<TelegramUser?> GetByChatIdAsync(long chatId);
    Task<TelegramUser?> GetWithOrdersAsync(int userId);
    Task<TelegramUser?> GetWithCartAsync(int userId);
    Task<IEnumerable<TelegramUser>> GetBlockedUsersAsync();
    Task<IEnumerable<TelegramUser>> GetAdminsAsync();
    Task<IEnumerable<TelegramUser>> GetUsersWithStateAsync(ConversationState state);
}
