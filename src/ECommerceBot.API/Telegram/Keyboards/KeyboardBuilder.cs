using ECommerceBot.API.Telegram.Services;
using Telegram.Bot.Types.ReplyMarkups;
using static ECommerceBot.API.Telegram.Services.SettingsCatalog;

namespace ECommerceBot.API.Telegram.Keyboards;

public class KeyboardBuilder : IKeyboardBuilder
{
    private readonly IBotTextService _texts;

    public KeyboardBuilder(IBotTextService texts)
    {
        _texts = texts;
    }

    public async Task<ReplyKeyboardMarkup> BuildMainMenuAsync(string lang = "fa")
    {
        var products = await _texts.GetAsync("MainMenu.ProductsButton", lang, "🛒 Products");
        var wallet   = await _texts.GetAsync("MainMenu.WalletButton",   lang, "💰 Wallet");
        var orders   = await _texts.GetAsync("MainMenu.OrdersButton",   lang, "📦 Orders");
        var support  = await _texts.GetAsync("MainMenu.SupportButton",  lang, "🎫 Support");
        var help     = await _texts.GetAsync("MainMenu.HelpButton",     lang, "❓ Help");

        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(products), new KeyboardButton(wallet) },
            new[] { new KeyboardButton(orders), new KeyboardButton(support), new KeyboardButton(help) }
        })
        { ResizeKeyboard = true };
    }

    public async Task<ReplyKeyboardMarkup> BuildAdminMenuAsync(string lang = "fa")
    {
        var orders     = await _texts.GetAsync("AdminMenu.OrdersButton",     lang, "📋 سفارش‌های در انتظار");
        var users      = await _texts.GetAsync("AdminMenu.UsersButton",      lang, "👥 کاربران");
        var products   = await _texts.GetAsync("AdminMenu.ProductsButton",   lang, "📦 محصولات");
        var categories = await _texts.GetAsync("AdminMenu.CategoriesButton", lang, "🗂 دسته‌بندی‌ها");
        var cards      = await _texts.GetAsync("AdminMenu.CardsButton",      lang, "💳 کارت‌های بانکی");
        var settings   = await _texts.GetAsync("AdminMenu.SettingsButton",   lang, "⚙️ تنظیمات");
        var stats      = await _texts.GetAsync("AdminMenu.StatisticsButton", lang, "📊 آمار");
        var admins     = await _texts.GetAsync("AdminMenu.AdminsButton",     lang, "👑 مدیریت ادمین‌ها");
        var userView   = await _texts.GetAsync("AdminMenu.UserViewButton",   lang, "👁 مشاهده مثل کاربر");
        var license    = await _texts.GetAsync("AdminMenu.LicenseButton",    lang, "🔐 وضعیت لایسنس");
        var coupons    = await _texts.GetAsync("AdminMenu.CouponsButton",    lang, "🎟 کوپن‌ها");
        var broadcast  = await _texts.GetAsync("AdminMenu.BroadcastButton",  lang, "📢 پیام همگانی");
        var export     = await _texts.GetAsync("AdminMenu.ExportButton",     lang, "📤 خروجی CSV");

        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(orders),    new KeyboardButton(users) },
            new[] { new KeyboardButton(products),  new KeyboardButton(categories) },
            new[] { new KeyboardButton(cards),     new KeyboardButton(settings) },
            new[] { new KeyboardButton(stats),     new KeyboardButton(admins) },
            new[] { new KeyboardButton(userView),  new KeyboardButton(license) },
            new[] { new KeyboardButton(coupons) },
            new[] { new KeyboardButton(broadcast), new KeyboardButton(export) }
        })
        { ResizeKeyboard = true };
    }

    public InlineKeyboardMarkup BuildCategoriesKeyboard(IEnumerable<(int Id, string Name)> categories)
    {
        var rows = categories
            .Select(c => new[] { InlineKeyboardButton.WithCallbackData(c.Name, $"cat:{c.Id}") })
            .ToList();
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup BuildProductsKeyboard(IEnumerable<(int Id, string Name, decimal Price)> products, int categoryId)
    {
        var rows = products
            .Select(p => new[] { InlineKeyboardButton.WithCallbackData($"{p.Name} — {p.Price:F0}$", $"prod:{p.Id}") })
            .ToList();
        return new InlineKeyboardMarkup(rows);
    }

    public async Task<InlineKeyboardMarkup> BuildOrderAdminActionsAsync(int orderId, string lang = "fa")
    {
        var approve    = await _texts.GetAsync("AdminActions.ApproveButton",            lang, "🟢 Approve");
        var reject     = await _texts.GetAsync("AdminActions.RejectButton",             lang, "🔴 Reject");
        var newReceipt = await _texts.GetAsync("AdminActions.RequestNewReceiptButton",  lang, "🔄 New Receipt");
        var refund     = await _texts.GetAsync("AdminActions.RefundButton",             lang, "💸 Refund");

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(approve, $"order:approve:{orderId}"),
                InlineKeyboardButton.WithCallbackData(reject,  $"order:reject:{orderId}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(newReceipt, $"order:newreceipt:{orderId}"),
                InlineKeyboardButton.WithCallbackData(refund,     $"order:refund:{orderId}")
            }
        });
    }

    public async Task<InlineKeyboardMarkup> BuildLicenseActionsKeyboardAsync(string lang = "fa")
    {
        var refresh     = await _texts.GetAsync("LicenseActions.RefreshButton",     lang, "🔄 Refresh");
        var activate    = await _texts.GetAsync("LicenseActions.ActivateButton",    lang, "🔑 Activate");
        var fingerprint = await _texts.GetAsync("LicenseActions.FingerprintButton", lang, "🖥 Server Fingerprint");

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(refresh,  "lic:refresh"),
                InlineKeyboardButton.WithCallbackData(activate, "lic:activate")
            },
            new[] { InlineKeyboardButton.WithCallbackData(fingerprint, "lic:fingerprint") }
        });
    }

    public async Task<InlineKeyboardMarkup> BuildBackButtonAsync(string callbackData = "menu:main", string lang = "fa")
    {
        var back = await _texts.GetAsync("Buttons.BackButton", lang, "⬅️ Back");
        return new InlineKeyboardMarkup(
            new[] { new[] { InlineKeyboardButton.WithCallbackData(back, callbackData) } });
    }

    public async Task<InlineKeyboardMarkup> BuildConfirmKeyboardAsync(string confirmData, string cancelData = "menu:main", string lang = "fa")
    {
        var confirm = await _texts.GetAsync("Buttons.ConfirmButton", lang, "✅ Confirm");
        var cancel  = await _texts.GetAsync("Buttons.CancelButton",  lang, "❌ Cancel");
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(confirm, confirmData),
                InlineKeyboardButton.WithCallbackData(cancel,  cancelData)
            }
        });
    }

    public async Task<ReplyKeyboardMarkup> BuildCancelKeyboardAsync(string lang = "fa")
    {
        var cancel = await _texts.GetAsync("Buttons.CancelButton", lang, "❌ لغو");
        return new ReplyKeyboardMarkup(cancel) { ResizeKeyboard = true };
    }

    public async Task<ReplyKeyboardMarkup> BuildSkipCancelKeyboardAsync(string lang = "fa")
    {
        var cancel = await _texts.GetAsync("Buttons.CancelButton", lang, "❌ لغو");
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("⏭️ رد شدن"), new KeyboardButton(cancel) }
        })
        { ResizeKeyboard = true };
    }

    public InlineKeyboardMarkup BuildSettingsCategoriesKeyboard()
    {
        var rows = SettingsCatalog.Categories
            .Select(cat => new[]
            {
                InlineKeyboardButton.WithCallbackData(cat, $"adm:set:cat:{Uri.EscapeDataString(cat)}")
            })
            .ToList();
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup BuildSettingsByCategoryKeyboard(string category)
    {
        var rows = SettingsCatalog.GetByCategory(category)
            .Select(kvp => new[]
            {
                InlineKeyboardButton.WithCallbackData(kvp.Value.PersianLabel, $"adm:set:{kvp.Key}")
            })
            .ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ بازگشت", "adm:set:cats") });
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup BuildCategoryPickerKeyboard(
        IEnumerable<(int Id, string Name)> categories, string callbackPrefix)
    {
        var rows = categories
            .Select(c => new[] { InlineKeyboardButton.WithCallbackData(c.Name, $"{callbackPrefix}:{c.Id}") })
            .ToList();
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup BuildCouponDiscountTypeKeyboard() =>
        new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💯 درصدی", "adm:cpn:dtype:pct"),
                InlineKeyboardButton.WithCallbackData("💵 مبلغ ثابت", "adm:cpn:dtype:fixed"),
            }
        });

    public async Task<ReplyKeyboardMarkup> BuildCouponOrSkipKeyboardAsync(string lang = "fa")
    {
        var skip   = "⏭️ بدون تخفیف";
        var cancel = await _texts.GetAsync("Buttons.CancelButton", lang, "❌ لغو");
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🎟 کد تخفیف دارم"), new KeyboardButton(skip) },
            new[] { new KeyboardButton(cancel) }
        })
        { ResizeKeyboard = true };
    }

    public InlineKeyboardMarkup BuildExportKeyboard() =>
        new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📦 خروجی سفارش‌ها", "adm:export:orders"),
                InlineKeyboardButton.WithCallbackData("👥 خروجی کاربران",  "adm:export:users")
            }
        });
}
