using Telegram.Bot.Types;

namespace ECommerceBot.API.Telegram.Handlers;

public interface ISuperAdminHandler
{
    Task HandleMessageAsync(Message message, CancellationToken ct = default);
    Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken ct = default);
}
