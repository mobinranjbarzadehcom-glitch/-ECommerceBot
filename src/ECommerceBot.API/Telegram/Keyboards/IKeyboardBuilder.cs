using Telegram.Bot.Types.ReplyMarkups;

namespace ECommerceBot.API.Telegram.Keyboards;

public interface IKeyboardBuilder
{
    Task<ReplyKeyboardMarkup> BuildMainMenuAsync(string lang = "fa");
    Task<ReplyKeyboardMarkup> BuildAdminMenuAsync(string lang = "fa");
    InlineKeyboardMarkup BuildCategoriesKeyboard(IEnumerable<(int Id, string Name)> categories);
    InlineKeyboardMarkup BuildProductsKeyboard(IEnumerable<(int Id, string Name, decimal Price)> products, int categoryId);
    Task<InlineKeyboardMarkup> BuildOrderAdminActionsAsync(int orderId, string lang = "fa");
    Task<InlineKeyboardMarkup> BuildBackButtonAsync(string callbackData = "menu:main", string lang = "fa");
    Task<InlineKeyboardMarkup> BuildConfirmKeyboardAsync(string confirmData, string cancelData = "menu:main", string lang = "fa");
    Task<InlineKeyboardMarkup> BuildLicenseActionsKeyboardAsync(string lang = "fa");
    Task<ReplyKeyboardMarkup> BuildCancelKeyboardAsync(string lang = "fa");
    Task<ReplyKeyboardMarkup> BuildSkipCancelKeyboardAsync(string lang = "fa");
    InlineKeyboardMarkup BuildSettingsCategoriesKeyboard();
    InlineKeyboardMarkup BuildSettingsByCategoryKeyboard(string category);
    InlineKeyboardMarkup BuildCategoryPickerKeyboard(IEnumerable<(int Id, string Name)> categories, string callbackPrefix);
    InlineKeyboardMarkup BuildCouponDiscountTypeKeyboard();
    Task<ReplyKeyboardMarkup> BuildCouponOrSkipKeyboardAsync(string lang = "fa");
    InlineKeyboardMarkup BuildExportKeyboard();
    InlineKeyboardMarkup BuildFaqListKeyboard(IEnumerable<(int Id, string Question)> items, bool isAdmin = false);
    InlineKeyboardMarkup BuildRenewalDurationKeyboard();
    InlineKeyboardMarkup BuildScheduledBroadcastFilterKeyboard();
}
