using Telegram.Bot;

namespace ECommerceBot.API.Infrastructure.Multitenancy;

public interface ITenantBotClientFactory
{
    ITelegramBotClient GetOrCreate(string decryptedToken);
}
