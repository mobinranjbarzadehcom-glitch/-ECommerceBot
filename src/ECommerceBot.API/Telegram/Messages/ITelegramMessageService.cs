using Telegram.Bot.Types.ReplyMarkups;

namespace ECommerceBot.API.Telegram.Messages;

public interface ITelegramMessageService
{
    Task SendHtmlAsync(long chatId, string html, IReplyMarkup? markup = null, CancellationToken ct = default);
    Task SendPhotoAsync(long chatId, string fileId, string? caption = null, IReplyMarkup? markup = null, CancellationToken ct = default);
    Task EditHtmlAsync(long chatId, int messageId, string html, InlineKeyboardMarkup? markup = null, CancellationToken ct = default);
    Task AnswerCallbackAsync(string callbackQueryId, string? text = null, bool showAlert = false, CancellationToken ct = default);
    Task DeleteMessageAsync(long chatId, int messageId, CancellationToken ct = default);
    Task NotifyAdminsAsync(string html, InlineKeyboardMarkup? markup = null, CancellationToken ct = default);
    Task NotifyAdminsWithPhotoAsync(string fileId, string caption, InlineKeyboardMarkup? markup = null, CancellationToken ct = default);
    Task ForwardToBackupAsync(long fromChatId, int messageId, CancellationToken ct = default);
    Task SendDocumentAsync(long chatId, byte[] content, string filename, string? caption = null, CancellationToken ct = default);
}
