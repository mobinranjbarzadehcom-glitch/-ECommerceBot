using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Telegram.Handlers;
using ECommerceBot.API.Telegram.Options;
using ECommerceBot.API.UnitOfWork;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ECommerceBot.API.Telegram;

public class UpdateDispatcher : IUpdateDispatcher
{
    private readonly IUnitOfWork _uow;
    private readonly ITenantContext _tenantContext;
    private readonly IMessageHandler _messageHandler;
    private readonly ICallbackQueryHandler _callbackHandler;
    private readonly ISuperAdminHandler _superAdminHandler;
    private readonly long[] _superAdminIds;
    private readonly ILogger<UpdateDispatcher> _logger;

    public UpdateDispatcher(
        IUnitOfWork uow,
        ITenantContext tenantContext,
        IMessageHandler messageHandler,
        ICallbackQueryHandler callbackHandler,
        ISuperAdminHandler superAdminHandler,
        IOptions<TelegramOptions> opts,
        ILogger<UpdateDispatcher> logger)
    {
        _uow = uow;
        _tenantContext = tenantContext;
        _messageHandler = messageHandler;
        _callbackHandler = callbackHandler;
        _superAdminHandler = superAdminHandler;
        _superAdminIds = opts.Value.SuperAdminChatIds;
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

        // Route to the SuperAdmin panel only when the update arrived via the platform bot
        // (legacy endpoint, TenantContext not set). If TenantContext IS set the update came
        // through a specific tenant webhook — even a SuperAdmin must be handled as a tenant
        // user so that they interact with that tenant, not the platform panel.
        if (IsSuperAdmin(telegramId.Value) && !_tenantContext.IsSet)
        {
            await _superAdminHandler.HandleMessageAsync(message, ct);
            return;
        }

        var user = await _uow.Users.GetByTelegramIdAsync(telegramId.Value);
        await _messageHandler.HandleAsync(message, user, ct);
    }

    private async Task DispatchCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken ct)
    {
        var telegramId = callbackQuery.From.Id;

        if (IsSuperAdmin(telegramId) && !_tenantContext.IsSet)
        {
            await _superAdminHandler.HandleCallbackAsync(callbackQuery, ct);
            return;
        }

        var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
        if (user is null)
        {
            _logger.LogWarning("Callback from unknown user {TelegramId}", telegramId);
            return;
        }

        await _callbackHandler.HandleAsync(callbackQuery, user, ct);
    }

    private bool IsSuperAdmin(long telegramId) =>
        _superAdminIds.Length > 0 && Array.IndexOf(_superAdminIds, telegramId) >= 0;
}
