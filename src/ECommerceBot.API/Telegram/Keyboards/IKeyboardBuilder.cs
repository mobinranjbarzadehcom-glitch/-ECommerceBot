using Telegram.Bot.Types.ReplyMarkups;

namespace ECommerceBot.API.Telegram.Keyboards;

public interface IKeyboardBuilder
{
    Task<ReplyKeyboardMarkup> BuildMainMenuAsync();
    Task<ReplyKeyboardMarkup> BuildAdminMenuAsync();
    InlineKeyboardMarkup BuildCategoriesKeyboard(IEnumerable<(int Id, string Name)> categories);
    InlineKeyboardMarkup BuildProductsKeyboard(IEnumerable<(int Id, string Name, decimal Price)> products, int categoryId);
    Task<InlineKeyboardMarkup> BuildOrderAdminActionsAsync(int orderId);
    InlineKeyboardMarkup BuildBackButton(string callbackData = "menu:main");
    InlineKeyboardMarkup BuildConfirmKeyboard(string confirmData, string cancelData = "menu:main");
    Task<InlineKeyboardMarkup> BuildLicenseActionsKeyboardAsync();
}
