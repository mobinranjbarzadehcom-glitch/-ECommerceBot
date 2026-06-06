using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ECommerceBot.API.Services.Implementations;

public class BroadcastService : IBroadcastService
{
    private readonly IUnitOfWork _uow;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<BroadcastService> _logger;

    public BroadcastService(IUnitOfWork uow, ITelegramBotClient bot, ILogger<BroadcastService> logger)
    {
        _uow = uow;
        _bot = bot;
        _logger = logger;
    }

    public async Task<ServiceResult<int>> SendBroadcastAsync(string htmlMessage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(htmlMessage))
            return ServiceResult<int>.Failure("متن پیام نمی‌تواند خالی باشد.");

        var users = await _uow.Users.GetAllAsync();
        var targets = users.Where(u => !u.IsBlocked && u.ChatId > 0).ToList();

        int sent = 0;
        foreach (var user in targets)
        {
            try
            {
                await _bot.SendMessage(
                    chatId: user.ChatId,
                    text: htmlMessage,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
                sent++;
                // Respect Telegram rate limit: ~30 messages/second per bot
                await Task.Delay(35, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broadcast failed for user {UserId} (ChatId={ChatId})", user.Id, user.ChatId);
            }
        }

        _logger.LogInformation("Broadcast sent: {Sent}/{Total}", sent, targets.Count);
        return ServiceResult<int>.Success(sent);
    }
}
