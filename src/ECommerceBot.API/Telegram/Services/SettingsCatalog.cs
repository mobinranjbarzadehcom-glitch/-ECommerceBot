namespace ECommerceBot.API.Telegram.Services;

/// <summary>
/// Maps raw BotSettings keys to Persian UI labels and display categories.
/// Used by the admin settings CMS to show human-readable names instead of raw keys.
/// </summary>
public static class SettingsCatalog
{
    public record Entry(string Category, string PersianLabel);

    public static readonly IReadOnlyDictionary<string, Entry> All =
        new Dictionary<string, Entry>
        {
            // ── General messages ─────────────────────────────────────────────
            ["WelcomeMessage"]             = new("📨 پیام‌های عمومی",    "پیام خوش‌آمدگویی"),
            ["HelpMessage"]                = new("📨 پیام‌های عمومی",    "پیام راهنما"),
            ["Menu.Title"]                 = new("📨 پیام‌های عمومی",    "عنوان منو"),

            // ── Payment ──────────────────────────────────────────────────────
            ["PaymentInstructionMessage"]  = new("💳 پرداخت",            "دستورالعمل پرداخت"),
            ["SupportWelcomeMessage"]      = new("🎫 پشتیبانی",          "پیام باز کردن تیکت"),

            // ── Main menu buttons ────────────────────────────────────────────
            ["MainMenu.ProductsButton"]    = new("🔘 منوی اصلی",         "دکمه محصولات"),
            ["MainMenu.WalletButton"]      = new("🔘 منوی اصلی",         "دکمه کیف پول"),
            ["MainMenu.OrdersButton"]      = new("🔘 منوی اصلی",         "دکمه سفارش‌ها"),
            ["MainMenu.SupportButton"]     = new("🔘 منوی اصلی",         "دکمه پشتیبانی"),
            ["MainMenu.HelpButton"]        = new("🔘 منوی اصلی",         "دکمه راهنما"),

            // ── Admin menu buttons ───────────────────────────────────────────
            ["AdminMenu.OrdersButton"]     = new("👑 منوی ادمین",        "دکمه سفارش‌های در انتظار"),
            ["AdminMenu.UsersButton"]      = new("👑 منوی ادمین",        "دکمه کاربران"),
            ["AdminMenu.ProductsButton"]   = new("👑 منوی ادمین",        "دکمه محصولات"),
            ["AdminMenu.CategoriesButton"] = new("👑 منوی ادمین",        "دکمه دسته‌بندی‌ها"),
            ["AdminMenu.CardsButton"]      = new("👑 منوی ادمین",        "دکمه کارت‌های بانکی"),
            ["AdminMenu.SettingsButton"]   = new("👑 منوی ادمین",        "دکمه تنظیمات"),
            ["AdminMenu.StatisticsButton"] = new("👑 منوی ادمین",        "دکمه آمار"),
            ["AdminMenu.AdminsButton"]     = new("👑 منوی ادمین",        "دکمه مدیریت ادمین‌ها"),
            ["AdminMenu.UserViewButton"]   = new("👑 منوی ادمین",        "دکمه مشاهده مثل کاربر"),
            ["AdminMenu.LicenseButton"]    = new("👑 منوی ادمین",        "دکمه وضعیت لایسنس"),

            // ── Shared buttons ───────────────────────────────────────────────
            ["Buttons.CancelButton"]       = new("🔵 دکمه‌های مشترک",   "دکمه لغو"),
            ["Buttons.BackButton"]         = new("🔵 دکمه‌های مشترک",   "دکمه بازگشت"),
            ["Buttons.ConfirmButton"]      = new("🔵 دکمه‌های مشترک",   "دکمه تأیید"),

            // ── Error messages ───────────────────────────────────────────────
            ["Errors.Blocked"]             = new("⚠️ پیام‌های خطا",     "خطای کاربر مسدود"),
            ["Errors.RateLimited"]         = new("⚠️ پیام‌های خطا",     "خطای سرعت زیاد"),
            ["Errors.PlayerIdEmpty"]       = new("⚠️ پیام‌های خطا",     "خطای شناسه بازیکن خالی"),

            // ── Order messages ───────────────────────────────────────────────
            ["OrderPendingMessage"]        = new("📦 پیام‌های سفارش",   "پیام انتظار بررسی"),
            ["OrderApprovedMessage"]       = new("📦 پیام‌های سفارش",   "پیام تأیید سفارش"),
            ["OrderRejectedMessage"]       = new("📦 پیام‌های سفارش",   "پیام رد سفارش"),
        };

    public static IEnumerable<string> Categories =>
        All.Values.Select(e => e.Category).Distinct();

    public static IEnumerable<KeyValuePair<string, Entry>> GetByCategory(string category) =>
        All.Where(e => e.Value.Category == category);

    public static string GetLabel(string key) =>
        All.TryGetValue(key, out var e) ? e.PersianLabel : key;
}
