using System.Collections.Concurrent;
using Telegram.Bot;

namespace ECommerceBot.API.Infrastructure.Multitenancy;

public class TenantBotClientFactory : ITenantBotClientFactory
{
    private readonly ConcurrentDictionary<string, ITelegramBotClient> _clients = new();

    public ITelegramBotClient GetOrCreate(string decryptedToken) =>
        _clients.GetOrAdd(decryptedToken, t => new TelegramBotClient(t));
}
