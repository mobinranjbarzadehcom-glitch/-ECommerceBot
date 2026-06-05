using ECommerceBot.API.Entities;
using Telegram.Bot.Types;

namespace ECommerceBot.API.Telegram.Handlers;

public interface ICallbackQueryHandler
{
    Task HandleAsync(CallbackQuery callbackQuery, TelegramUser user, CancellationToken ct = default);
}
