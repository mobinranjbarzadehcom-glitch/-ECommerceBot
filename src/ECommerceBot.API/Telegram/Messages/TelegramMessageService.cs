using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Telegram.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgTypes = Telegram.Bot.Types;

namespace ECommerceBot.API.Telegram.Messages;

public class TelegramMessageService : ITelegramMessageService
{
    private readonly ITelegramBotClient _bot;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramMessageService> _logger;

    public TelegramMessageService(
        ITelegramBotClient defaultBotClient,
        ITenantBotClientFactory clientFactory,
        ITenantContext tenantContext,
        IOptions<TelegramOptions> options,
        ILogger<TelegramMessageService> logger)
    {
        // Use tenant-specific client when available; otherwise fall back to the default singleton.
        _bot = tenantContext.IsSet && !string.IsNullOrEmpty(tenantContext.BotToken)
            ? clientFactory.GetOrCreate(tenantContext.BotToken)
            : defaultBotClient;

        _options = options.Value;
        _logger = logger;
    }

    public async Task SendHtmlAsync(long chatId, string html, IReplyMarkup? markup = null, CancellationToken ct = default)
    {
        try
        {
            await _bot.SendMessage(chatId, html, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send HTML message to {ChatId}", chatId);
        }
    }

    public async Task SendPhotoAsync(long chatId, string fileId, string? caption = null, IReplyMarkup? markup = null, CancellationToken ct = default)
    {
        try
        {
            await _bot.SendPhoto(chatId, TgTypes.InputFile.FromFileId(fileId),
                caption: caption, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send photo to {ChatId}", chatId);
        }
    }

    public async Task EditHtmlAsync(long chatId, int messageId, string html, InlineKeyboardMarkup? markup = null, CancellationToken ct = default)
    {
        try
        {
            await _bot.EditMessageText(chatId, messageId, html, parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit message {MessageId} for {ChatId}", messageId, chatId);
        }
    }

    public async Task AnswerCallbackAsync(string callbackQueryId, string? text = null, bool showAlert = false, CancellationToken ct = default)
    {
        try
        {
            await _bot.AnswerCallbackQuery(callbackQueryId, text, showAlert: showAlert, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer callback query {Id}", callbackQueryId);
        }
    }

    public async Task DeleteMessageAsync(long chatId, int messageId, CancellationToken ct = default)
    {
        try
        {
            await _bot.DeleteMessage(chatId, messageId, cancellationToken: ct);
        }
        catch { /* ignore if message already deleted */ }
    }

    public async Task NotifyAdminsAsync(string html, InlineKeyboardMarkup? markup = null, CancellationToken ct = default)
    {
        foreach (var adminChatId in _options.AdminChatIds)
        {
            await SendHtmlAsync(adminChatId, html, markup, ct);
        }
    }

    public async Task NotifyAdminsWithPhotoAsync(string fileId, string caption, InlineKeyboardMarkup? markup = null, CancellationToken ct = default)
    {
        foreach (var adminChatId in _options.AdminChatIds)
        {
            await SendPhotoAsync(adminChatId, fileId, caption, markup, ct);
        }
    }

    public async Task ForwardToBackupAsync(long fromChatId, int messageId, CancellationToken ct = default)
    {
        if (_options.BackupChannelId == 0) return;
        try
        {
            await _bot.ForwardMessage(_options.BackupChannelId, fromChatId, messageId, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward message to backup channel");
        }
    }

    public async Task SendDocumentAsync(long chatId, byte[] content, string filename, string? caption = null, CancellationToken ct = default)
    {
        try
        {
            using var stream = new MemoryStream(content);
            await _bot.SendDocument(
                chatId: chatId,
                document: TgTypes.InputFile.FromStream(stream, filename),
                caption: caption,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send document '{Filename}' to {ChatId}", filename, chatId);
        }
    }
}
