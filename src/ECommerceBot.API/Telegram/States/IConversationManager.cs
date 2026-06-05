using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Telegram.States;

public interface IConversationManager
{
    Task SetStateAsync(TelegramUser user, ConversationState state, CancellationToken ct = default);
    Task ClearStateAsync(TelegramUser user, CancellationToken ct = default);
    Task<OrderContext?> GetOrderContextAsync(TelegramUser user);
    Task SetOrderContextAsync(TelegramUser user, OrderContext context, CancellationToken ct = default);
    Task<AdminContext?> GetAdminContextAsync(TelegramUser user);
    Task SetAdminContextAsync(TelegramUser user, AdminContext context, CancellationToken ct = default);
    Task UpdateActivityAsync(TelegramUser user, CancellationToken ct = default);
}
