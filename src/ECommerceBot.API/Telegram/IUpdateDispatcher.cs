using Telegram.Bot.Types;

namespace ECommerceBot.API.Telegram;

public interface IUpdateDispatcher
{
    Task DispatchAsync(Update update, CancellationToken ct = default);
}
