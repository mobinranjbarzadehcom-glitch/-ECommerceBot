using ECommerceBot.API.Entities;
using Telegram.Bot.Types;

namespace ECommerceBot.API.Telegram.Handlers;

public interface IMessageHandler
{
    Task HandleAsync(Message message, TelegramUser? user, CancellationToken ct = default);
}
