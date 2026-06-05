using ECommerceBot.API.Infrastructure.Cache;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Telegram.Services;

public class BotTextService : IBotTextService
{
    // ── Localised defaults — Persian (fa) ─────────────────────────────────────
    private static readonly Dictionary<string, string> Defaults = new()
    {
        // ── Persian messages ──────────────────────────────────────────────────
        ["WelcomeMessage.fa"] = "👋 <b>به ECommerceBot خوش آمدید!</b>\n\nاز منوی زیر شروع کنید.",
        ["HelpMessage.fa"] = "📋 <b>راهنما</b>\n\n• <b>محصولات</b> — خرید آیتم‌های بازی\n• <b>کیف پول</b> — مدیریت موجودی\n• <b>سفارشات</b> — پیگیری خرید\n• <b>پشتیبانی</b> — ارتباط با ما",
        ["SupportWelcomeMessage.fa"] = "🎫 <b>پشتیبانی</b>\n\nپیام خود را ارسال کنید. به‌زودی پاسخ می‌دهیم.",
        ["PaymentInstructionMessage.fa"] = "💳 <b>راهنمای پرداخت</b>\n\nلطفاً <b>{amount}</b> را به کارت نشان داده شده واریز و تصویر رسید را ارسال کنید.",
        ["OrderPendingMessage.fa"] = "⏳ <b>سفارش #{orderId} ثبت شد</b>\n\nسفارش شما در حال بررسی است. به‌زودی اطلاع می‌دهیم.",
        ["OrderApprovedMessage.fa"] = "✅ <b>سفارش #{orderId} تأیید شد!</b>\n\nکدهای محصول:\n\n{keys}",
        ["OrderRejectedMessage.fa"] = "❌ <b>سفارش #{orderId} رد شد</b>\n\nدلیل: {reason}",
        ["OrderExpiredMessage.fa"] = "⏰ <b>سفارش #{orderId} منقضی شد</b>\n\nسفارش شما به‌موقع تأیید نشد.",
        ["MainMenu.ProductsButton.fa"] = "🛒 محصولات",
        ["MainMenu.WalletButton.fa"] = "💰 کیف پول",
        ["MainMenu.OrdersButton.fa"] = "📦 سفارشات",
        ["MainMenu.SupportButton.fa"] = "🎫 پشتیبانی",
        ["MainMenu.HelpButton.fa"] = "❓ راهنما",
        ["AdminMenu.OrdersButton.fa"] = "📋 سفارشات معلق",
        ["AdminMenu.UsersButton.fa"] = "👥 کاربران",
        ["AdminMenu.ProductsButton.fa"] = "📦 محصولات",
        ["AdminMenu.CategoriesButton.fa"] = "🗂 دسته‌بندی‌ها",
        ["AdminMenu.CardsButton.fa"] = "💳 کارت‌های بانکی",
        ["AdminMenu.SettingsButton.fa"] = "⚙️ تنظیمات",
        ["AdminMenu.StatisticsButton.fa"] = "📊 آمار",
        ["AdminMenu.LicenseButton.fa"] = "🔐 وضعیت لایسنس",
        ["AdminActions.ApproveButton.fa"] = "🟢 تأیید",
        ["AdminActions.RejectButton.fa"] = "🔴 رد",
        ["AdminActions.RequestNewReceiptButton.fa"] = "🔄 رسید جدید",
        ["AdminActions.MessageUserButton.fa"] = "✉️ پیام",
        ["AdminActions.RefundButton.fa"] = "💸 استرداد",
        // License
        ["License.InvalidMessage.fa"] = "⛔ <b>لایسنس نامعتبر است</b>\n\nخدمات موقتاً در دسترس نیست. با فروشنده تماس بگیرید.",
        ["License.ExpiredMessage.fa"] = "⏰ <b>لایسنس منقضی شده است</b>\n\nبرای تمدید با فروشنده تماس بگیرید.",
        ["License.GracePeriodMessage.fa"] = "⚠️ <b>لایسنس در دوره اطلاع‌رسانی است</b>\n\nلطفاً هرچه سریع‌تر تمدید کنید.",
        ["License.ActivationSuccessMessage.fa"] = "✅ <b>لایسنس با موفقیت فعال شد!</b>\n\nنسخه: {edition}\nمالک: {owner}\nانقضا: {expiresAt}",
        ["License.ActivationFailedMessage.fa"] = "❌ <b>فعال‌سازی لایسنس ناموفق بود</b>\n\nخطا: {error}",
        // Brand
        ["Brand.Name"] = "ECommerceBot",
        ["Brand.ShortName"] = "ECBot",
        ["Brand.SupportUsername"] = "",
        ["Brand.WebsiteUrl"] = "",
        ["Brand.LogoFileId"] = "",
        ["Brand.FooterText.fa"] = "پشتیبانی ۲۴ ساعته در خدمت شما",
        ["Brand.FooterText.en"] = "24/7 support at your service",
        ["Brand.PoweredByText"] = "Powered by ECommerceBot",
        ["Brand.ShowPoweredBy"] = "true",
        ["Brand.PrimaryEmoji"] = "🛒",
        ["Brand.SuccessEmoji"] = "✅",
        ["Brand.WarningEmoji"] = "⚠️",
        ["Brand.ErrorEmoji"] = "❌",

        // ── English messages (en) ──────────────────────────────────────────────
        ["WelcomeMessage.en"] = "👋 <b>Welcome to ECommerceBot!</b>\n\nUse the menu below to get started.",
        ["HelpMessage.en"] = "📋 <b>Help</b>\n\n• Use <b>Products</b> to browse and buy\n• Use <b>Wallet</b> to manage balance\n• Use <b>Orders</b> to track purchases\n• Use <b>Support</b> to contact us",
        ["SupportWelcomeMessage.en"] = "🎫 <b>Support</b>\n\nSend your message and we'll get back to you shortly.",
        ["PaymentInstructionMessage.en"] = "💳 <b>Payment Instructions</b>\n\nPlease transfer <b>{amount}</b> to the card shown and send us the receipt photo.",
        ["OrderPendingMessage.en"] = "⏳ <b>Order #{orderId} Submitted</b>\n\nYour order is under review. We'll notify you shortly.",
        ["OrderApprovedMessage.en"] = "✅ <b>Order #{orderId} Approved!</b>\n\nYour product keys:\n\n{keys}",
        ["OrderRejectedMessage.en"] = "❌ <b>Order #{orderId} Rejected</b>\n\nReason: {reason}",
        ["OrderExpiredMessage.en"] = "⏰ <b>Order #{orderId} Expired</b>\n\nYour order was not confirmed in time.",
        ["MainMenu.ProductsButton.en"] = "🛒 Products",
        ["MainMenu.WalletButton.en"] = "💰 Wallet",
        ["MainMenu.OrdersButton.en"] = "📦 Orders",
        ["MainMenu.SupportButton.en"] = "🎫 Support",
        ["MainMenu.HelpButton.en"] = "❓ Help",
        ["AdminMenu.LicenseButton.en"] = "🔐 License Status",
        ["License.InvalidMessage.en"] = "⛔ <b>License Invalid</b>\n\nService temporarily unavailable. Please contact your vendor.",
        ["License.ExpiredMessage.en"] = "⏰ <b>License Expired</b>\n\nPlease contact your vendor to renew.",
        ["License.GracePeriodMessage.en"] = "⚠️ <b>License in Grace Period</b>\n\nPlease renew as soon as possible.",
        ["License.ActivationSuccessMessage.en"] = "✅ <b>License Activated!</b>\n\nEdition: {edition}\nOwner: {owner}\nExpires: {expiresAt}",
        ["License.ActivationFailedMessage.en"] = "❌ <b>License Activation Failed</b>\n\nError: {error}",

        // ── Language-neutral base keys (existing defaults) ─────────────────────
        ["WelcomeMessage"] = "👋 <b>Welcome to ECommerceBot!</b>\n\nUse the menu below to get started.",
        ["HelpMessage"] = "📋 <b>Help</b>\n\n• Use <b>Products</b> to browse and buy\n• Use <b>Wallet</b> to check your balance\n• Use <b>Orders</b> to track purchases\n• Use <b>Support</b> to contact us",
        ["SupportWelcomeMessage"] = "🎫 <b>Support</b>\n\nSend your message and we'll get back to you shortly.",
        ["PaymentInstructionMessage"] = "💳 <b>Payment Instructions</b>\n\nPlease transfer <b>{amount}</b> to the card shown and send us the receipt photo.",
        ["OrderPendingMessage"] = "⏳ <b>Order #{orderId} Submitted</b>\n\nYour order is under review. We'll notify you shortly.",
        ["OrderApprovedMessage"] = "✅ <b>Order #{orderId} Approved!</b>\n\nYour product keys:\n\n{keys}",
        ["OrderRejectedMessage"] = "❌ <b>Order #{orderId} Rejected</b>\n\nReason: {reason}",
        ["OrderExpiredMessage"] = "⏰ <b>Order #{orderId} Expired</b>\n\nYour order was not confirmed in time.",
        ["MainMenu.ProductsButton"] = "🛒 Products",
        ["MainMenu.WalletButton"] = "💰 Wallet",
        ["MainMenu.OrdersButton"] = "📦 Orders",
        ["MainMenu.SupportButton"] = "🎫 Support",
        ["MainMenu.HelpButton"] = "❓ Help",
        ["AdminMenu.OrdersButton"] = "📋 Pending Orders",
        ["AdminMenu.UsersButton"] = "👥 Users",
        ["AdminMenu.ProductsButton"] = "📦 Products",
        ["AdminMenu.CategoriesButton"] = "🗂 Categories",
        ["AdminMenu.CardsButton"] = "💳 Cards",
        ["AdminMenu.SettingsButton"] = "⚙️ Settings",
        ["AdminMenu.StatisticsButton"] = "📊 Statistics",
        ["AdminMenu.LicenseButton"] = "🔐 وضعیت لایسنس",
        ["AdminActions.ApproveButton"] = "🟢 Approve",
        ["AdminActions.RejectButton"] = "🔴 Reject",
        ["AdminActions.RequestNewReceiptButton"] = "🔄 Request New Receipt",
        ["AdminActions.MessageUserButton"] = "✉️ Message User",
        ["AdminActions.RefundButton"] = "💸 Refund"
    };

    // Cache key pattern: botsettings:{key}
    private const string CacheKeyPrefix = "botsettings:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public BotTextService(IUnitOfWork uow, ICacheService cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        var cacheKey = CacheKeyPrefix + key;

        var cached = await _cache.GetAsync(cacheKey);
        if (cached is not null)
            return cached;

        var stored = await _uow.BotSettings.GetValueAsync(key);
        // <tg-emoji> and other HTML is preserved as-is through the cache round-trip
        var value = stored
            ?? (Defaults.TryGetValue(key, out var def) ? def : defaultValue);

        await _cache.SetAsync(cacheKey, value, CacheDuration);
        return value;
    }

    public async Task<string> FormatAsync(string key, Dictionary<string, string> vars, string defaultValue = "")
    {
        var template = await GetAsync(key, defaultValue);
        foreach (var (k, v) in vars)
            template = template.Replace($"{{{k}}}", v);
        return template;
    }

    public async Task SetAsync(string key, string value)
    {
        await _uow.BotSettings.UpsertAsync(key, value);
        await _uow.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKeyPrefix + key);
    }
}
