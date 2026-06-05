using ECommerceBot.API.Telegram.Services;
using Telegram.Bot.Types.ReplyMarkups;

namespace ECommerceBot.API.Telegram.Keyboards;

public class KeyboardBuilder : IKeyboardBuilder
{
    private readonly IBotTextService _texts;

    public KeyboardBuilder(IBotTextService texts)
    {
        _texts = texts;
    }

    public async Task<ReplyKeyboardMarkup> BuildMainMenuAsync()
    {
        var products = await _texts.GetAsync("MainMenu.ProductsButton", "🛒 Products");
        var wallet = await _texts.GetAsync("MainMenu.WalletButton", "💰 Wallet");
        var orders = await _texts.GetAsync("MainMenu.OrdersButton", "📦 Orders");
        var support = await _texts.GetAsync("MainMenu.SupportButton", "🎫 Support");
        var help = await _texts.GetAsync("MainMenu.HelpButton", "❓ Help");

        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(products), new KeyboardButton(wallet) },
            new[] { new KeyboardButton(orders), new KeyboardButton(support), new KeyboardButton(help) }
        })
        { ResizeKeyboard = true };
    }

    public async Task<ReplyKeyboardMarkup> BuildAdminMenuAsync()
    {
        var orders = await _texts.GetAsync("AdminMenu.OrdersButton", "📋 Pending Orders");
        var users = await _texts.GetAsync("AdminMenu.UsersButton", "👥 Users");
        var products = await _texts.GetAsync("AdminMenu.ProductsButton", "📦 Products");
        var categories = await _texts.GetAsync("AdminMenu.CategoriesButton", "🗂 Categories");
        var cards = await _texts.GetAsync("AdminMenu.CardsButton", "💳 Cards");
        var settings = await _texts.GetAsync("AdminMenu.SettingsButton", "⚙️ Settings");
        var stats = await _texts.GetAsync("AdminMenu.StatisticsButton", "📊 Statistics");

        var license = await _texts.GetAsync("AdminMenu.LicenseButton", "🔐 وضعیت لایسنس");

        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(orders), new KeyboardButton(users) },
            new[] { new KeyboardButton(products), new KeyboardButton(categories) },
            new[] { new KeyboardButton(cards), new KeyboardButton(settings), new KeyboardButton(stats) },
            new[] { new KeyboardButton(license) }
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
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back", "menu:products") });
        return new InlineKeyboardMarkup(rows);
    }

    public async Task<InlineKeyboardMarkup> BuildOrderAdminActionsAsync(int orderId)
    {
        var approve = await _texts.GetAsync("AdminActions.ApproveButton", "🟢 Approve");
        var reject = await _texts.GetAsync("AdminActions.RejectButton", "🔴 Reject");
        var newReceipt = await _texts.GetAsync("AdminActions.RequestNewReceiptButton", "🔄 New Receipt");
        var msgUser = await _texts.GetAsync("AdminActions.MessageUserButton", "✉️ Message");
        var refund = await _texts.GetAsync("AdminActions.RefundButton", "💸 Refund");

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(approve, $"order:approve:{orderId}"),
                InlineKeyboardButton.WithCallbackData(reject, $"order:reject:{orderId}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(newReceipt, $"order:newreceipt:{orderId}"),
                InlineKeyboardButton.WithCallbackData(refund, $"order:refund:{orderId}")
            }
        });
    }

    public async Task<InlineKeyboardMarkup> BuildLicenseActionsKeyboardAsync()
    {
        var refresh = await _texts.GetAsync("LicenseActions.RefreshButton", "🔄 بررسی مجدد");
        var activate = await _texts.GetAsync("LicenseActions.ActivateButton", "🔑 فعال‌سازی");
        var fingerprint = await _texts.GetAsync("LicenseActions.FingerprintButton", "🖥 اثر انگشت سرور");

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(refresh, "lic:refresh"),
                InlineKeyboardButton.WithCallbackData(activate, "lic:activate")
            },
            new[] { InlineKeyboardButton.WithCallbackData(fingerprint, "lic:fingerprint") }
        });
    }

    public InlineKeyboardMarkup BuildBackButton(string callbackData = "menu:main") =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back", callbackData) } });

    public InlineKeyboardMarkup BuildConfirmKeyboard(string confirmData, string cancelData = "menu:main") =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Confirm", confirmData),
                InlineKeyboardButton.WithCallbackData("❌ Cancel", cancelData)
            }
        });
}
