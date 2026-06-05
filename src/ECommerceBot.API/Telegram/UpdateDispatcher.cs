using ECommerceBot.API.Telegram.Handlers;
using ECommerceBot.API.UnitOfWork;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ECommerceBot.API.Telegram;

public class UpdateDispatcher : IUpdateDispatcher
{
    private readonly IUnitOfWork _uow;
    private readonly IMessageHandler _messageHandler;
    private readonly ICallbackQueryHandler _callbackHandler;
    private readonly ILogger<UpdateDispatcher> _logger;

    public UpdateDispatcher(
        IUnitOfWork uow,
        IMessageHandler messageHandler,
        ICallbackQueryHandler callbackHandler,
        ILogger<UpdateDispatcher> logger)
    {
        _uow = uow;
        _messageHandler = messageHandler;
        _callbackHandler = callbackHandler;
        _logger = logger;
    }

    public async Task DispatchAsync(Update update, CancellationToken ct = default)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message when update.Message is not null:
                    await DispatchMessageAsync(update.Message, ct);
                    break;
                case UpdateType.CallbackQuery when update.CallbackQuery is not null:
                    await DispatchCallbackQueryAsync(update.CallbackQuery, ct);
                    break;
                default:
                    _logger.LogDebug("Unhandled update type: {Type}", update.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dispatching update {UpdateId}", update.Id);
        }
    }

    private async Task DispatchMessageAsync(Message message, CancellationToken ct)
    {
        var telegramId = message.From?.Id;
        if (telegramId is null) return;

        var user = await _uow.Users.GetByTelegramIdAsync(telegramId.Value);
        await _messageHandler.HandleAsync(message, user, ct);
    }

    private async Task DispatchCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken ct)
    {
        var telegramId = callbackQuery.From.Id;
        var user = await _uow.Users.GetByTelegramIdAsync(telegramId);

        if (user is null)
        {
            _logger.LogWarning("Callback from unknown user {TelegramId}", telegramId);
            return;
        }

        await _callbackHandler.HandleAsync(callbackQuery, user, ct);
    }
}
