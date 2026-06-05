using ECommerceBot.API.Infrastructure.Cache;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Telegram.Services;

public class BotTextService : IBotTextService
{
    // ── Defaults dictionary ───────────────────────────────────────────────────
    // Convention: key.fa = Persian, key.en = English, key (no suffix) = English fallback
    private static readonly Dictionary<string, string> Defaults = new()
    {
        // ── Brand ─────────────────────────────────────────────────────────────
        ["Brand.Name"]          = "ECommerceBot",
        ["Brand.ShortName"]     = "ECBot",
        ["Brand.SupportUsername"] = "",
        ["Brand.WebsiteUrl"]    = "",
        ["Brand.LogoFileId"]    = "",
        ["Brand.FooterText.fa"] = "پشتیبانی ۲۴ ساعته در خدمت شما",
        ["Brand.FooterText.en"] = "24/7 support at your service",
        ["Brand.PoweredByText"] = "Powered by ECommerceBot",
        ["Brand.ShowPoweredBy"] = "true",
        ["Brand.PrimaryEmoji"]  = "🛒",
        ["Brand.SuccessEmoji"]  = "✅",
        ["Brand.WarningEmoji"]  = "⚠️",
        ["Brand.ErrorEmoji"]    = "❌",

        // ── Shared user messages ──────────────────────────────────────────────
        ["WelcomeMessage"]    = "👋 <b>Welcome to ECommerceBot!</b>\n\nUse the menu below to get started.",
        ["WelcomeMessage.fa"] = "👋 <b>به ECommerceBot خوش آمدید!</b>\n\nاز منوی زیر شروع کنید.",
        ["WelcomeMessage.en"] = "👋 <b>Welcome to ECommerceBot!</b>\n\nUse the menu below to get started.",

        ["HelpMessage"]    = "📋 <b>Help</b>\n\n• Use <b>Products</b> to browse and buy\n• Use <b>Wallet</b> to check your balance\n• Use <b>Orders</b> to track purchases\n• Use <b>Support</b> to contact us",
        ["HelpMessage.fa"] = "📋 <b>راهنما</b>\n\n• <b>محصولات</b> — خرید آیتم‌های بازی\n• <b>کیف پول</b> — مدیریت موجودی\n• <b>سفارشات</b> — پیگیری خرید\n• <b>پشتیبانی</b> — ارتباط با ما",
        ["HelpMessage.en"] = "📋 <b>Help</b>\n\n• Use <b>Products</b> to browse and buy\n• Use <b>Wallet</b> to manage balance\n• Use <b>Orders</b> to track purchases\n• Use <b>Support</b> to contact us",

        ["SupportWelcomeMessage"]    = "🎫 <b>Support</b>\n\nSend your message and we'll get back to you shortly.",
        ["SupportWelcomeMessage.fa"] = "🎫 <b>پشتیبانی</b>\n\nپیام خود را ارسال کنید. به‌زودی پاسخ می‌دهیم.",
        ["SupportWelcomeMessage.en"] = "🎫 <b>Support</b>\n\nSend your message and we'll get back to you shortly.",

        ["PaymentInstructionMessage"]    = "💳 <b>Payment Instructions</b>\n\nPlease transfer <b>{amount}</b> to the card shown and send us the receipt photo.",
        ["PaymentInstructionMessage.fa"] = "💳 <b>راهنمای پرداخت</b>\n\nلطفاً <b>{amount}</b> را به کارت نشان داده شده واریز و تصویر رسید را ارسال کنید.",
        ["PaymentInstructionMessage.en"] = "💳 <b>Payment Instructions</b>\n\nPlease transfer <b>{amount}</b> to the card shown and send us the receipt photo.",

        ["OrderPendingMessage"]    = "⏳ <b>Order #{orderId} Submitted</b>\n\nYour order is under review. We'll notify you shortly.",
        ["OrderPendingMessage.fa"] = "⏳ <b>سفارش #{orderId} ثبت شد</b>\n\nسفارش شما در حال بررسی است. به‌زودی اطلاع می‌دهیم.",
        ["OrderPendingMessage.en"] = "⏳ <b>Order #{orderId} Submitted</b>\n\nYour order is under review. We'll notify you shortly.",

        ["OrderApprovedMessage"]    = "✅ <b>Order #{orderId} Approved!</b>\n\nYour product keys:\n\n{keys}",
        ["OrderApprovedMessage.fa"] = "✅ <b>سفارش #{orderId} تأیید شد!</b>\n\nکدهای محصول:\n\n{keys}",
        ["OrderApprovedMessage.en"] = "✅ <b>Order #{orderId} Approved!</b>\n\nYour product keys:\n\n{keys}",

        ["OrderRejectedMessage"]    = "❌ <b>Order #{orderId} Rejected</b>\n\nReason: {reason}",
        ["OrderRejectedMessage.fa"] = "❌ <b>سفارش #{orderId} رد شد</b>\n\nدلیل: {reason}",
        ["OrderRejectedMessage.en"] = "❌ <b>Order #{orderId} Rejected</b>\n\nReason: {reason}",

        ["OrderExpiredMessage"]    = "⏰ <b>Order #{orderId} Expired</b>\n\nYour order was not confirmed in time.",
        ["OrderExpiredMessage.fa"] = "⏰ <b>سفارش #{orderId} منقضی شد</b>\n\nسفارش شما به‌موقع تأیید نشد.",
        ["OrderExpiredMessage.en"] = "⏰ <b>Order #{orderId} Expired</b>\n\nYour order was not confirmed in time.",

        // ── Error messages ────────────────────────────────────────────────────
        ["Errors.PleaseStart"]    = "Please send /start to begin.",
        ["Errors.PleaseStart.fa"] = "لطفاً /start را ارسال کنید.",
        ["Errors.PleaseStart.en"] = "Please send /start to begin.",

        ["Errors.Blocked"]    = "❌ You are blocked from using this bot.",
        ["Errors.Blocked.fa"] = "❌ شما از این بات مسدود شده‌اید.",
        ["Errors.Blocked.en"] = "❌ You are blocked from using this bot.",

        ["Errors.RateLimited"]    = "⚠️ Too many messages. Please wait a moment before trying again.",
        ["Errors.RateLimited.fa"] = "⚠️ پیام‌های زیاد. لطفاً کمی صبر کنید.",
        ["Errors.RateLimited.en"] = "⚠️ Too many messages. Please wait a moment before trying again.",

        ["Errors.UseMenuForReceipt"]    = "Please use the menu to start an order before sending a receipt.",
        ["Errors.UseMenuForReceipt.fa"] = "لطفاً ابتدا از منو سفارش ثبت کنید، سپس رسید ارسال نمایید.",
        ["Errors.UseMenuForReceipt.en"] = "Please use the menu to start an order before sending a receipt.",

        ["Errors.UseMenuButtons"]    = "Please use the menu buttons.",
        ["Errors.UseMenuButtons.fa"] = "لطفاً از دکمه‌های منو استفاده کنید.",
        ["Errors.UseMenuButtons.en"] = "Please use the menu buttons.",

        ["Errors.PlayerIdEmpty"]    = "❌ Player ID cannot be empty. Please try again:",
        ["Errors.PlayerIdEmpty.fa"] = "❌ شناسه بازیکن نمی‌تواند خالی باشد. مجدداً وارد کنید:",
        ["Errors.PlayerIdEmpty.en"] = "❌ Player ID cannot be empty. Please try again:",

        ["Errors.ReceiptNotPhoto"]    = "📸 Please send the receipt as a photo.",
        ["Errors.ReceiptNotPhoto.fa"] = "📸 لطفاً رسید را به‌صورت عکس ارسال کنید.",
        ["Errors.ReceiptNotPhoto.en"] = "📸 Please send the receipt as a photo.",

        ["Errors.SessionExpired"]    = "❌ Session expired. Please start over.",
        ["Errors.SessionExpired.fa"] = "❌ نشست منقضی شد. لطفاً دوباره شروع کنید.",
        ["Errors.SessionExpired.en"] = "❌ Session expired. Please start over.",

        ["Errors.TicketMessageEmpty"]    = "❌ Message cannot be empty.",
        ["Errors.TicketMessageEmpty.fa"] = "❌ پیام نمی‌تواند خالی باشد.",
        ["Errors.TicketMessageEmpty.en"] = "❌ Message cannot be empty.",

        // ── Common buttons ────────────────────────────────────────────────────
        ["Buttons.CancelButton"]    = "❌ Cancel",
        ["Buttons.CancelButton.fa"] = "❌ انصراف",
        ["Buttons.CancelButton.en"] = "❌ Cancel",

        ["Buttons.BackButton"]    = "⬅️ Back",
        ["Buttons.BackButton.fa"] = "⬅️ بازگشت",
        ["Buttons.BackButton.en"] = "⬅️ Back",

        ["Buttons.ConfirmButton"]    = "✅ Confirm",
        ["Buttons.ConfirmButton.fa"] = "✅ تأیید",
        ["Buttons.ConfirmButton.en"] = "✅ Confirm",

        // ── Menu labels ───────────────────────────────────────────────────────
        ["Menu.Title"]    = "📋 <b>Menu</b>",
        ["Menu.Title.fa"] = "📋 <b>منو</b>",
        ["Menu.Title.en"] = "📋 <b>Menu</b>",

        // ── Main menu buttons ─────────────────────────────────────────────────
        ["MainMenu.ProductsButton"]    = "🛒 Products",
        ["MainMenu.ProductsButton.fa"] = "🛒 محصولات",
        ["MainMenu.ProductsButton.en"] = "🛒 Products",

        ["MainMenu.WalletButton"]    = "💰 Wallet",
        ["MainMenu.WalletButton.fa"] = "💰 کیف پول",
        ["MainMenu.WalletButton.en"] = "💰 Wallet",

        ["MainMenu.OrdersButton"]    = "📦 Orders",
        ["MainMenu.OrdersButton.fa"] = "📦 سفارشات",
        ["MainMenu.OrdersButton.en"] = "📦 Orders",

        ["MainMenu.SupportButton"]    = "🎫 Support",
        ["MainMenu.SupportButton.fa"] = "🎫 پشتیبانی",
        ["MainMenu.SupportButton.en"] = "🎫 Support",

        ["MainMenu.HelpButton"]    = "❓ Help",
        ["MainMenu.HelpButton.fa"] = "❓ راهنما",
        ["MainMenu.HelpButton.en"] = "❓ Help",

        // ── Admin menu buttons ────────────────────────────────────────────────
        ["AdminMenu.OrdersButton"]    = "📋 Pending Orders",
        ["AdminMenu.OrdersButton.fa"] = "📋 سفارشات معلق",
        ["AdminMenu.OrdersButton.en"] = "📋 Pending Orders",

        ["AdminMenu.UsersButton"]    = "👥 Users",
        ["AdminMenu.UsersButton.fa"] = "👥 کاربران",
        ["AdminMenu.UsersButton.en"] = "👥 Users",

        ["AdminMenu.ProductsButton"]    = "📦 Products",
        ["AdminMenu.ProductsButton.fa"] = "📦 محصولات",
        ["AdminMenu.ProductsButton.en"] = "📦 Products",

        ["AdminMenu.CategoriesButton"]    = "🗂 Categories",
        ["AdminMenu.CategoriesButton.fa"] = "🗂 دسته‌بندی‌ها",
        ["AdminMenu.CategoriesButton.en"] = "🗂 Categories",

        ["AdminMenu.CardsButton"]    = "💳 Cards",
        ["AdminMenu.CardsButton.fa"] = "💳 کارت‌های بانکی",
        ["AdminMenu.CardsButton.en"] = "💳 Cards",

        ["AdminMenu.SettingsButton"]    = "⚙️ Settings",
        ["AdminMenu.SettingsButton.fa"] = "⚙️ تنظیمات",
        ["AdminMenu.SettingsButton.en"] = "⚙️ Settings",

        ["AdminMenu.StatisticsButton"]    = "📊 Statistics",
        ["AdminMenu.StatisticsButton.fa"] = "📊 آمار",
        ["AdminMenu.StatisticsButton.en"] = "📊 Statistics",

        ["AdminMenu.LicenseButton"]    = "🔐 License Status",
        ["AdminMenu.LicenseButton.fa"] = "🔐 وضعیت لایسنس",
        ["AdminMenu.LicenseButton.en"] = "🔐 License Status",

        // ── Admin action buttons ──────────────────────────────────────────────
        ["AdminActions.ApproveButton"]    = "🟢 Approve",
        ["AdminActions.ApproveButton.fa"] = "🟢 تأیید",
        ["AdminActions.ApproveButton.en"] = "🟢 Approve",

        ["AdminActions.RejectButton"]    = "🔴 Reject",
        ["AdminActions.RejectButton.fa"] = "🔴 رد",
        ["AdminActions.RejectButton.en"] = "🔴 Reject",

        ["AdminActions.RequestNewReceiptButton"]    = "🔄 Request New Receipt",
        ["AdminActions.RequestNewReceiptButton.fa"] = "🔄 رسید جدید",
        ["AdminActions.RequestNewReceiptButton.en"] = "🔄 Request New Receipt",

        ["AdminActions.MessageUserButton"]    = "✉️ Message User",
        ["AdminActions.MessageUserButton.fa"] = "✉️ پیام",
        ["AdminActions.MessageUserButton.en"] = "✉️ Message User",

        ["AdminActions.RefundButton"]    = "💸 Refund",
        ["AdminActions.RefundButton.fa"] = "💸 استرداد",
        ["AdminActions.RefundButton.en"] = "💸 Refund",

        // ── License actions buttons ───────────────────────────────────────────
        ["LicenseActions.RefreshButton"]    = "🔄 Refresh",
        ["LicenseActions.RefreshButton.fa"] = "🔄 بررسی مجدد",
        ["LicenseActions.RefreshButton.en"] = "🔄 Refresh",

        ["LicenseActions.ActivateButton"]    = "🔑 Activate",
        ["LicenseActions.ActivateButton.fa"] = "🔑 فعال‌سازی",
        ["LicenseActions.ActivateButton.en"] = "🔑 Activate",

        ["LicenseActions.FingerprintButton"]    = "🖥 Server Fingerprint",
        ["LicenseActions.FingerprintButton.fa"] = "🖥 اثر انگشت سرور",
        ["LicenseActions.FingerprintButton.en"] = "🖥 Server Fingerprint",

        // ── Products / categories ─────────────────────────────────────────────
        ["Products.NoCategoriesAvailable"]    = "😔 No categories available at the moment.",
        ["Products.NoCategoriesAvailable.fa"] = "😔 دسته‌بندی‌ای موجود نیست.",
        ["Products.NoCategoriesAvailable.en"] = "😔 No categories available at the moment.",

        ["Products.SelectCategory"]    = "🛒 <b>Select a category:</b>",
        ["Products.SelectCategory.fa"] = "🛒 <b>یک دسته‌بندی انتخاب کنید:</b>",
        ["Products.SelectCategory.en"] = "🛒 <b>Select a category:</b>",

        ["Products.CategoryNotFound"]    = "❌ Category not found or disabled.",
        ["Products.CategoryNotFound.fa"] = "❌ دسته‌بندی پیدا نشد یا غیرفعال است.",
        ["Products.CategoryNotFound.en"] = "❌ Category not found or disabled.",

        ["Products.NoneInCategory"]    = "😔 No products in this category.",
        ["Products.NoneInCategory.fa"] = "😔 محصولی در این دسته‌بندی وجود ندارد.",
        ["Products.NoneInCategory.en"] = "😔 No products in this category.",

        ["Products.SelectProduct"]    = "🛒 <b>{name}</b>\n\nSelect a product:",
        ["Products.SelectProduct.fa"] = "🛒 <b>{name}</b>\n\nیک محصول انتخاب کنید:",
        ["Products.SelectProduct.en"] = "🛒 <b>{name}</b>\n\nSelect a product:",

        ["Products.NotFound"]    = "❌ Product not found or unavailable.",
        ["Products.NotFound.fa"] = "❌ محصول پیدا نشد یا در دسترس نیست.",
        ["Products.NotFound.en"] = "❌ Product not found or unavailable.",

        ["Products.OutOfStock"]    = "❌ <i>Out of stock</i>",
        ["Products.OutOfStock.fa"] = "❌ <i>موجودی ندارد</i>",
        ["Products.OutOfStock.en"] = "❌ <i>Out of stock</i>",

        ["Products.EnterPlayerId"]    = "🎮 <b>Please enter your Player ID / Account details:</b>",
        ["Products.EnterPlayerId.fa"] = "🎮 <b>لطفاً شناسه بازیکن / اطلاعات حساب خود را وارد کنید:</b>",
        ["Products.EnterPlayerId.en"] = "🎮 <b>Please enter your Player ID / Account details:</b>",

        // ── Wallet ────────────────────────────────────────────────────────────
        ["Wallet.Title"]    = "💰 <b>Wallet</b>\n\nBalance: <b>{balance}$</b>",
        ["Wallet.Title.fa"] = "💰 <b>کیف پول</b>\n\nموجودی: <b>{balance}$</b>",
        ["Wallet.Title.en"] = "💰 <b>Wallet</b>\n\nBalance: <b>{balance}$</b>",

        ["Wallet.RecentTransactions"]    = "📋 <b>Recent Transactions:</b>",
        ["Wallet.RecentTransactions.fa"] = "📋 <b>تراکنش‌های اخیر:</b>",
        ["Wallet.RecentTransactions.en"] = "📋 <b>Recent Transactions:</b>",

        // ── Orders ────────────────────────────────────────────────────────────
        ["Orders.Empty"]    = "📦 You have no orders yet.",
        ["Orders.Empty.fa"] = "📦 هنوز سفارشی ندارید.",
        ["Orders.Empty.en"] = "📦 You have no orders yet.",

        ["Orders.RecentTitle"]    = "📦 <b>Your Recent Orders:</b>",
        ["Orders.RecentTitle.fa"] = "📦 <b>سفارشات اخیر شما:</b>",
        ["Orders.RecentTitle.en"] = "📦 <b>Your Recent Orders:</b>",

        // ── Tickets ───────────────────────────────────────────────────────────
        ["Ticket.SubjectFormat"]    = "Support from {name}",
        ["Ticket.SubjectFormat.fa"] = "پشتیبانی از {name}",
        ["Ticket.SubjectFormat.en"] = "Support from {name}",

        ["Ticket.CreatedSuccess"]    = "✅ <b>Ticket #{ticketId} created!</b> We'll respond soon.",
        ["Ticket.CreatedSuccess.fa"] = "✅ <b>تیکت #{ticketId} ایجاد شد!</b> به‌زودی پاسخ می‌دهیم.",
        ["Ticket.CreatedSuccess.en"] = "✅ <b>Ticket #{ticketId} created!</b> We'll respond soon.",

        // ── Refund notification ───────────────────────────────────────────────
        ["User.RefundReceived"]    = "💸 <b>Refund received!</b> {amount}$ has been added to your wallet.",
        ["User.RefundReceived.fa"] = "💸 <b>استرداد دریافت شد!</b> {amount}$ به کیف پول شما اضافه شد.",
        ["User.RefundReceived.en"] = "💸 <b>Refund received!</b> {amount}$ has been added to your wallet.",

        // ── Callback validation ───────────────────────────────────────────────
        ["Callback.InvalidAction"]    = "Invalid action.",
        ["Callback.InvalidAction.fa"] = "عملیات نامعتبر.",
        ["Callback.InvalidAction.en"] = "Invalid action.",

        ["Callback.Blocked"]    = "You are blocked.",
        ["Callback.Blocked.fa"] = "شما مسدود شده‌اید.",
        ["Callback.Blocked.en"] = "You are blocked.",

        // ── License messages ──────────────────────────────────────────────────
        ["License.InvalidMessage"]    = "⛔ <b>License Invalid</b>\n\nService temporarily unavailable. Please contact your vendor.",
        ["License.InvalidMessage.fa"] = "⛔ <b>لایسنس نامعتبر است</b>\n\nخدمات موقتاً در دسترس نیست. با فروشنده تماس بگیرید.",
        ["License.InvalidMessage.en"] = "⛔ <b>License Invalid</b>\n\nService temporarily unavailable. Please contact your vendor.",

        ["License.ExpiredMessage"]    = "⏰ <b>License Expired</b>\n\nPlease contact your vendor to renew.",
        ["License.ExpiredMessage.fa"] = "⏰ <b>لایسنس منقضی شده است</b>\n\nبرای تمدید با فروشنده تماس بگیرید.",
        ["License.ExpiredMessage.en"] = "⏰ <b>License Expired</b>\n\nPlease contact your vendor to renew.",

        ["License.GracePeriodMessage"]    = "⚠️ <b>License in Grace Period</b>\n\nPlease renew as soon as possible.",
        ["License.GracePeriodMessage.fa"] = "⚠️ <b>لایسنس در دوره اطلاع‌رسانی است</b>\n\nلطفاً هرچه سریع‌تر تمدید کنید.",
        ["License.GracePeriodMessage.en"] = "⚠️ <b>License in Grace Period</b>\n\nPlease renew as soon as possible.",

        ["License.ActivationSuccessMessage"]    = "✅ <b>License Activated!</b>\n\nEdition: {edition}\nOwner: {owner}\nExpires: {expiresAt}",
        ["License.ActivationSuccessMessage.fa"] = "✅ <b>لایسنس با موفقیت فعال شد!</b>\n\nنسخه: {edition}\nمالک: {owner}\nانقضا: {expiresAt}",
        ["License.ActivationSuccessMessage.en"] = "✅ <b>License Activated!</b>\n\nEdition: {edition}\nOwner: {owner}\nExpires: {expiresAt}",

        ["License.ActivationFailedMessage"]    = "❌ <b>License Activation Failed</b>\n\nError: {error}",
        ["License.ActivationFailedMessage.fa"] = "❌ <b>فعال‌سازی لایسنس ناموفق بود</b>\n\nخطا: {error}",
        ["License.ActivationFailedMessage.en"] = "❌ <b>License Activation Failed</b>\n\nError: {error}",

        ["License.RefreshStatus"]    = "🔄 <b>License Status Updated</b>\n\nStatus: <b>{status}</b>\n{message}",
        ["License.RefreshStatus.fa"] = "🔄 <b>بروزرسانی وضعیت لایسنس</b>\n\nوضعیت: <b>{status}</b>\n{message}",
        ["License.RefreshStatus.en"] = "🔄 <b>License Status Updated</b>\n\nStatus: <b>{status}</b>\n{message}",

        ["License.ActivatePrompt"]    = "🔑 <b>License Activation</b>\n\nPlease enter the full license key:",
        ["License.ActivatePrompt.fa"] = "🔑 <b>فعال‌سازی لایسنس</b>\n\nلطفاً کد لایسنس کامل را وارد کنید:",
        ["License.ActivatePrompt.en"] = "🔑 <b>License Activation</b>\n\nPlease enter the full license key:",

        ["License.FingerprintTitle"]    = "🖥 <b>Server Fingerprint</b>\n\n<code>{fingerprint}</code>\n\nProvide this to your vendor to bind the license to this server.",
        ["License.FingerprintTitle.fa"] = "🖥 <b>اثر انگشت سرور</b>\n\n<code>{fingerprint}</code>\n\nاین مقدار را به فروشنده ارائه دهید تا لایسنس را به این سرور متصل کند.",
        ["License.FingerprintTitle.en"] = "🖥 <b>Server Fingerprint</b>\n\n<code>{fingerprint}</code>\n\nProvide this to your vendor to bind the license to this server.",

        ["License.UnknownAction"]    = "❌ Unknown action.",
        ["License.UnknownAction.fa"] = "❌ عملیات ناشناخته.",
        ["License.UnknownAction.en"] = "❌ Unknown action.",

        // ── Admin-only messages ───────────────────────────────────────────────
        ["Admin.Only"]    = "❌ Admin only.",
        ["Admin.Only.fa"] = "❌ فقط ادمین.",
        ["Admin.Only.en"] = "❌ Admin only.",

        ["Admin.TooManyActions"]    = "⚠️ Too many admin actions. Please wait a minute.",
        ["Admin.TooManyActions.fa"] = "⚠️ عملیات زیاد. لطفاً یک دقیقه صبر کنید.",
        ["Admin.TooManyActions.en"] = "⚠️ Too many admin actions. Please wait a minute.",

        ["Admin.UseMenu"]    = "Please use the menu.",
        ["Admin.UseMenu.fa"] = "لطفاً از منو استفاده کنید.",
        ["Admin.UseMenu.en"] = "Please use the menu.",

        ["Admin.NoPendingOrders"]    = "✅ No pending orders.",
        ["Admin.NoPendingOrders.fa"] = "✅ سفارش معلقی وجود ندارد.",
        ["Admin.NoPendingOrders.en"] = "✅ No pending orders.",

        ["Admin.PendingOrdersTitle"]    = "📋 <b>{count} Pending Orders:</b>",
        ["Admin.PendingOrdersTitle.fa"] = "📋 <b>{count} سفارش معلق:</b>",
        ["Admin.PendingOrdersTitle.en"] = "📋 <b>{count} Pending Orders:</b>",

        ["Admin.UsersTitle"]    = "👥 <b>Users ({count} total)</b>",
        ["Admin.UsersTitle.fa"] = "👥 <b>کاربران ({count} نفر)</b>",
        ["Admin.UsersTitle.en"] = "👥 <b>Users ({count} total)</b>",

        ["Admin.NoProductsFound"]    = "No products found.",
        ["Admin.NoProductsFound.fa"] = "محصولی پیدا نشد.",
        ["Admin.NoProductsFound.en"] = "No products found.",

        ["Admin.ProductsTitle"]    = "📦 <b>Products:</b>",
        ["Admin.ProductsTitle.fa"] = "📦 <b>محصولات:</b>",
        ["Admin.ProductsTitle.en"] = "📦 <b>Products:</b>",

        ["Admin.AddProduct"]    = "➕ Add Product",
        ["Admin.AddProduct.fa"] = "➕ افزودن محصول",
        ["Admin.AddProduct.en"] = "➕ Add Product",

        ["Admin.CategoriesTitle"]    = "🗂 <b>Categories:</b>",
        ["Admin.CategoriesTitle.fa"] = "🗂 <b>دسته‌بندی‌ها:</b>",
        ["Admin.CategoriesTitle.en"] = "🗂 <b>Categories:</b>",

        ["Admin.AddCategory"]    = "➕ Add Category",
        ["Admin.AddCategory.fa"] = "➕ افزودن دسته‌بندی",
        ["Admin.AddCategory.en"] = "➕ Add Category",

        ["Admin.CardsTitle"]    = "💳 <b>Payment Cards:</b>",
        ["Admin.CardsTitle.fa"] = "💳 <b>کارت‌های بانکی:</b>",
        ["Admin.CardsTitle.en"] = "💳 <b>Payment Cards:</b>",

        ["Admin.AddCard"]    = "➕ Add Card",
        ["Admin.AddCard.fa"] = "➕ افزودن کارت",
        ["Admin.AddCard.en"] = "➕ Add Card",

        ["Admin.SettingsTitle"]    = "⚙️ <b>Bot Settings:</b>",
        ["Admin.SettingsTitle.fa"] = "⚙️ <b>تنظیمات بات:</b>",
        ["Admin.SettingsTitle.en"] = "⚙️ <b>Bot Settings:</b>",

        ["Admin.StatsTitle"]    = "📊 <b>Statistics</b>",
        ["Admin.StatsTitle.fa"] = "📊 <b>آمار</b>",
        ["Admin.StatsTitle.en"] = "📊 <b>Statistics</b>",

        // Admin error messages
        ["Admin.Errors.ReasonEmpty"]      = "❌ Reason cannot be empty.",
        ["Admin.Errors.ReasonEmpty.fa"]   = "❌ دلیل نمی‌تواند خالی باشد.",
        ["Admin.Errors.ReasonEmpty.en"]   = "❌ Reason cannot be empty.",

        ["Admin.Errors.NameEmpty"]        = "❌ Name cannot be empty.",
        ["Admin.Errors.NameEmpty.fa"]     = "❌ نام نمی‌تواند خالی باشد.",
        ["Admin.Errors.NameEmpty.en"]     = "❌ Name cannot be empty.",

        ["Admin.Errors.TitleEmpty"]       = "❌ Title cannot be empty.",
        ["Admin.Errors.TitleEmpty.fa"]    = "❌ عنوان نمی‌تواند خالی باشد.",
        ["Admin.Errors.TitleEmpty.en"]    = "❌ Title cannot be empty.",

        ["Admin.Errors.InvalidPrice"]     = "❌ Invalid price. Enter a positive number.",
        ["Admin.Errors.InvalidPrice.fa"]  = "❌ قیمت نامعتبر است. یک عدد مثبت وارد کنید.",
        ["Admin.Errors.InvalidPrice.en"]  = "❌ Invalid price. Enter a positive number.",

        ["Admin.Errors.InvalidCardNumber"]    = "❌ Invalid card number.",
        ["Admin.Errors.InvalidCardNumber.fa"] = "❌ شماره کارت نامعتبر است.",
        ["Admin.Errors.InvalidCardNumber.en"] = "❌ Invalid card number.",

        ["Admin.Errors.InvalidName"]      = "❌ Invalid name.",
        ["Admin.Errors.InvalidName.fa"]   = "❌ نام نامعتبر است.",
        ["Admin.Errors.InvalidName.en"]   = "❌ Invalid name.",

        ["Admin.Errors.InvalidBankName"]      = "❌ Invalid bank name.",
        ["Admin.Errors.InvalidBankName.fa"]   = "❌ نام بانک نامعتبر است.",
        ["Admin.Errors.InvalidBankName.en"]   = "❌ Invalid bank name.",

        ["Admin.Errors.ValueEmpty"]       = "❌ Value cannot be empty.",
        ["Admin.Errors.ValueEmpty.fa"]    = "❌ مقدار نمی‌تواند خالی باشد.",
        ["Admin.Errors.ValueEmpty.en"]    = "❌ Value cannot be empty.",

        ["Admin.Errors.MessageEmpty"]     = "❌ Message cannot be empty.",
        ["Admin.Errors.MessageEmpty.fa"]  = "❌ پیام نمی‌تواند خالی باشد.",
        ["Admin.Errors.MessageEmpty.en"]  = "❌ Message cannot be empty.",

        // Admin prompts
        ["Admin.CardHolderPrompt"]    = "👤 Enter cardholder name:",
        ["Admin.CardHolderPrompt.fa"] = "👤 نام دارنده کارت را وارد کنید:",
        ["Admin.CardHolderPrompt.en"] = "👤 Enter cardholder name:",

        ["Admin.BankPrompt"]    = "🏦 Enter bank name:",
        ["Admin.BankPrompt.fa"] = "🏦 نام بانک را وارد کنید:",
        ["Admin.BankPrompt.en"] = "🏦 Enter bank name:",

        ["Admin.RejectPrompt"]    = "📝 Enter rejection reason for Order #{orderId}:",
        ["Admin.RejectPrompt.fa"] = "📝 دلیل رد سفارش #{orderId} را وارد کنید:",
        ["Admin.RejectPrompt.en"] = "📝 Enter rejection reason for Order #{orderId}:",

        ["Admin.CatEnterName"]    = "📝 Enter new category name:",
        ["Admin.CatEnterName.fa"] = "📝 نام دسته‌بندی جدید را وارد کنید:",
        ["Admin.CatEnterName.en"] = "📝 Enter new category name:",

        ["Admin.CatEnterNewName"]    = "📝 Enter new name for <b>{name}</b>:",
        ["Admin.CatEnterNewName.fa"] = "📝 نام جدید برای <b>{name}</b> را وارد کنید:",
        ["Admin.CatEnterNewName.en"] = "📝 Enter new name for <b>{name}</b>:",

        ["Admin.ProdEnterTitle"]    = "📝 Enter new title for <b>{name}</b>:",
        ["Admin.ProdEnterTitle.fa"] = "📝 عنوان جدید برای <b>{name}</b> را وارد کنید:",
        ["Admin.ProdEnterTitle.en"] = "📝 Enter new title for <b>{name}</b>:",

        ["Admin.ProdEnterPrice"]    = "💰 Enter new price for <b>{name}</b>:",
        ["Admin.ProdEnterPrice.fa"] = "💰 قیمت جدید برای <b>{name}</b> را وارد کنید:",
        ["Admin.ProdEnterPrice.en"] = "💰 Enter new price for <b>{name}</b>:",

        ["Admin.CardEnterNumber"]    = "💳 Enter card number:",
        ["Admin.CardEnterNumber.fa"] = "💳 شماره کارت را وارد کنید:",
        ["Admin.CardEnterNumber.en"] = "💳 Enter card number:",

        ["Admin.SettingSelectPrompt"]    = "⚙️ Select a setting to edit:",
        ["Admin.SettingSelectPrompt.fa"] = "⚙️ یک تنظیم برای ویرایش انتخاب کنید:",
        ["Admin.SettingSelectPrompt.en"] = "⚙️ Select a setting to edit:",

        ["Admin.SettingEditPrompt"]    = "⚙️ <b>Editing:</b> <code>{key}</code>\n\nCurrent value:\n{current}\n\nSend new value:",
        ["Admin.SettingEditPrompt.fa"] = "⚙️ <b>ویرایش:</b> <code>{key}</code>\n\nمقدار فعلی:\n{current}\n\nمقدار جدید را ارسال کنید:",
        ["Admin.SettingEditPrompt.en"] = "⚙️ <b>Editing:</b> <code>{key}</code>\n\nCurrent value:\n{current}\n\nSend new value:",

        ["Admin.AdminMessageToUser"]    = "📨 <b>Message from Admin:</b>\n\n{text}",
        ["Admin.AdminMessageToUser.fa"] = "📨 <b>پیام از ادمین:</b>\n\n{text}",
        ["Admin.AdminMessageToUser.en"] = "📨 <b>Message from Admin:</b>\n\n{text}",

        // Admin success confirmations
        ["Admin.CardNumberUpdated"]    = "✅ Card number updated.",
        ["Admin.CardNumberUpdated.fa"] = "✅ شماره کارت به‌روزرسانی شد.",
        ["Admin.CardNumberUpdated.en"] = "✅ Card number updated.",

        ["Admin.CardAdded"]    = "✅ Payment card added.",
        ["Admin.CardAdded.fa"] = "✅ کارت بانکی اضافه شد.",
        ["Admin.CardAdded.en"] = "✅ Payment card added.",

        ["Admin.MessageSent"]    = "✅ Message sent.",
        ["Admin.MessageSent.fa"] = "✅ پیام ارسال شد.",
        ["Admin.MessageSent.en"] = "✅ Message sent.",

        ["Admin.CategoryCreated"]    = "✅ Category <b>{name}</b> created.",
        ["Admin.CategoryCreated.fa"] = "✅ دسته‌بندی <b>{name}</b> ایجاد شد.",
        ["Admin.CategoryCreated.en"] = "✅ Category <b>{name}</b> created.",

        ["Admin.CategoryRenamed"]    = "✅ Category renamed to <b>{name}</b>.",
        ["Admin.CategoryRenamed.fa"] = "✅ دسته‌بندی به <b>{name}</b> تغییر نام داد.",
        ["Admin.CategoryRenamed.en"] = "✅ Category renamed to <b>{name}</b>.",

        ["Admin.ProductRenamed"]    = "✅ Product renamed to <b>{title}</b>.",
        ["Admin.ProductRenamed.fa"] = "✅ محصول به <b>{title}</b> تغییر نام داد.",
        ["Admin.ProductRenamed.en"] = "✅ Product renamed to <b>{title}</b>.",

        ["Admin.ProductPriceSet"]    = "✅ Product price set to <b>{price}$</b>.",
        ["Admin.ProductPriceSet.fa"] = "✅ قیمت محصول به <b>{price}$</b> تنظیم شد.",
        ["Admin.ProductPriceSet.en"] = "✅ Product price set to <b>{price}$</b>.",

        ["Admin.SettingUpdated"]    = "✅ Setting <b>{key}</b> updated.",
        ["Admin.SettingUpdated.fa"] = "✅ تنظیم <b>{key}</b> به‌روزرسانی شد.",
        ["Admin.SettingUpdated.en"] = "✅ Setting <b>{key}</b> updated.",

        ["Admin.OrderRejected"]    = "✅ Order #{orderId} rejected.",
        ["Admin.OrderRejected.fa"] = "✅ سفارش #{orderId} رد شد.",
        ["Admin.OrderRejected.en"] = "✅ Order #{orderId} rejected.",

        ["Admin.OrderApproved"]    = "✅ Order #{orderId} approved.",
        ["Admin.OrderApproved.fa"] = "✅ سفارش #{orderId} تأیید شد.",
        ["Admin.OrderApproved.en"] = "✅ Order #{orderId} approved.",

        ["Admin.NewReceiptRequested"]    = "✅ New receipt requested from user for Order #{orderId}.",
        ["Admin.NewReceiptRequested.fa"] = "✅ درخواست رسید جدید از کاربر برای سفارش #{orderId} ارسال شد.",
        ["Admin.NewReceiptRequested.en"] = "✅ New receipt requested from user for Order #{orderId}.",

        ["Admin.UserNewReceiptRequest"]    = "🔄 <b>Admin is requesting a new receipt</b> for Order #{orderId}.\n\nPlease send a new receipt photo.",
        ["Admin.UserNewReceiptRequest.fa"] = "🔄 <b>ادمین در حال درخواست رسید جدید</b> برای سفارش #{orderId} است.\n\nلطفاً عکس رسید جدید ارسال کنید.",
        ["Admin.UserNewReceiptRequest.en"] = "🔄 <b>Admin is requesting a new receipt</b> for Order #{orderId}.\n\nPlease send a new receipt photo.",

        ["Admin.OrderNotFound"]    = "❌ Order not found.",
        ["Admin.OrderNotFound.fa"] = "❌ سفارش پیدا نشد.",
        ["Admin.OrderNotFound.en"] = "❌ Order not found.",

        ["Admin.RefundSuccess"]    = "✅ Refunded {amount}$ to user for Order #{orderId}.",
        ["Admin.RefundSuccess.fa"] = "✅ {amount}$ به کاربر برای سفارش #{orderId} مسترد شد.",
        ["Admin.RefundSuccess.en"] = "✅ Refunded {amount}$ to user for Order #{orderId}.",

        // Admin category inline buttons
        ["Admin.CatRenameButton"]    = "✏️ Rename",
        ["Admin.CatRenameButton.fa"] = "✏️ تغییر نام",
        ["Admin.CatRenameButton.en"] = "✏️ Rename",

        ["Admin.CatDisableButton"]    = "🔴 Disable",
        ["Admin.CatDisableButton.fa"] = "🔴 غیرفعال",
        ["Admin.CatDisableButton.en"] = "🔴 Disable",

        ["Admin.CatEnableButton"]    = "🟢 Enable",
        ["Admin.CatEnableButton.fa"] = "🟢 فعال",
        ["Admin.CatEnableButton.en"] = "🟢 Enable",

        ["Admin.CatEnabled"]    = "enabled",
        ["Admin.CatEnabled.fa"] = "فعال",
        ["Admin.CatEnabled.en"] = "enabled",

        ["Admin.CatDisabled"]    = "disabled",
        ["Admin.CatDisabled.fa"] = "غیرفعال",
        ["Admin.CatDisabled.en"] = "disabled",

        ["Admin.CatToggled"]    = "✅ Category <b>{name}</b> is now {status}.",
        ["Admin.CatToggled.fa"] = "✅ دسته‌بندی <b>{name}</b> اکنون {status} است.",
        ["Admin.CatToggled.en"] = "✅ Category <b>{name}</b> is now {status}.",

        ["Admin.CatNotFound"]    = "❌ Category not found.",
        ["Admin.CatNotFound.fa"] = "❌ دسته‌بندی پیدا نشد.",
        ["Admin.CatNotFound.en"] = "❌ Category not found.",

        // Admin product inline buttons
        ["Admin.ProdAddInfo"]    = "ℹ️ To add a product, please use the web CMS. Telegram-based product creation coming soon.",
        ["Admin.ProdAddInfo.fa"] = "ℹ️ برای افزودن محصول از CMS وب استفاده کنید. افزودن از تلگرام به‌زودی اضافه می‌شود.",
        ["Admin.ProdAddInfo.en"] = "ℹ️ To add a product, please use the web CMS. Telegram-based product creation coming soon.",

        ["Admin.ProdNotFound"]    = "❌ Product not found.",
        ["Admin.ProdNotFound.fa"] = "❌ محصول پیدا نشد.",
        ["Admin.ProdNotFound.en"] = "❌ Product not found.",

        ["Admin.ProdToggled"]    = "✅ Product <b>{name}</b> is now {status}.",
        ["Admin.ProdToggled.fa"] = "✅ محصول <b>{name}</b> اکنون {status} است.",
        ["Admin.ProdToggled.en"] = "✅ Product <b>{name}</b> is now {status}.",

        ["Admin.ProdRenameButton"]    = "✏️ Rename",
        ["Admin.ProdRenameButton.fa"] = "✏️ تغییر نام",
        ["Admin.ProdRenameButton.en"] = "✏️ Rename",

        ["Admin.ProdPriceButton"]    = "💰 Price",
        ["Admin.ProdPriceButton.fa"] = "💰 قیمت",
        ["Admin.ProdPriceButton.en"] = "💰 Price",

        ["Admin.ProdDisableButton"]    = "🔴 Disable",
        ["Admin.ProdDisableButton.fa"] = "🔴 غیرفعال",
        ["Admin.ProdDisableButton.en"] = "🔴 Disable",

        ["Admin.ProdEnableButton"]    = "🟢 Enable",
        ["Admin.ProdEnableButton.fa"] = "🟢 فعال",
        ["Admin.ProdEnableButton.en"] = "🟢 Enable",

        // Admin card inline buttons
        ["Admin.CardNotFound"]    = "❌ Card not found.",
        ["Admin.CardNotFound.fa"] = "❌ کارت پیدا نشد.",
        ["Admin.CardNotFound.en"] = "❌ Card not found.",

        ["Admin.CardToggled"]    = "✅ Card {number} is now {status}.",
        ["Admin.CardToggled.fa"] = "✅ کارت {number} اکنون {status} است.",
        ["Admin.CardToggled.en"] = "✅ Card {number} is now {status}.",

        ["Admin.CardActive"]    = "active",
        ["Admin.CardActive.fa"] = "فعال",
        ["Admin.CardActive.en"] = "active",

        ["Admin.CardInactive"]    = "inactive",
        ["Admin.CardInactive.fa"] = "غیرفعال",
        ["Admin.CardInactive.en"] = "inactive",

        ["Admin.CardSetDefault"]    = "✅ Card {number} set as default.",
        ["Admin.CardSetDefault.fa"] = "✅ کارت {number} به‌عنوان پیش‌فرض تنظیم شد.",
        ["Admin.CardSetDefault.en"] = "✅ Card {number} set as default.",

        ["Admin.CardDisableButton"]    = "🔴 Disable",
        ["Admin.CardDisableButton.fa"] = "🔴 غیرفعال",
        ["Admin.CardDisableButton.en"] = "🔴 Disable",

        ["Admin.CardEnableButton"]    = "🟢 Enable",
        ["Admin.CardEnableButton.fa"] = "🟢 فعال",
        ["Admin.CardEnableButton.en"] = "🟢 Enable",

        ["Admin.CardSetDefaultButton"]    = "⭐ Set Default",
        ["Admin.CardSetDefaultButton.fa"] = "⭐ پیش‌فرض",
        ["Admin.CardSetDefaultButton.en"] = "⭐ Set Default",

        // Admin license-related
        ["Admin.LicenseKeyEmpty"]    = "❌ License key is empty.",
        ["Admin.LicenseKeyEmpty.fa"] = "❌ کلید لایسنس خالی است.",
        ["Admin.LicenseKeyEmpty.en"] = "❌ License key is empty.",

        ["Admin.LicenseTrial"]    = "⚠️ <b>Trial License</b>",
        ["Admin.LicenseTrial.fa"] = "⚠️ <b>لایسنس آزمایشی</b>",
        ["Admin.LicenseTrial.en"] = "⚠️ <b>Trial License</b>",

        ["Admin.LicenseNoExpiry"]    = "♾ No expiration",
        ["Admin.LicenseNoExpiry.fa"] = "♾ بدون انقضا",
        ["Admin.LicenseNoExpiry.en"] = "♾ No expiration",

        ["Admin.LicenseStatusTitle"]    = "🔐 <b>License Status</b>",
        ["Admin.LicenseStatusTitle.fa"] = "🔐 <b>وضعیت لایسنس</b>",
        ["Admin.LicenseStatusTitle.en"] = "🔐 <b>License Status</b>",

        ["Admin.LicenseStatusLabel"]    = "Status",
        ["Admin.LicenseStatusLabel.fa"] = "وضعیت",
        ["Admin.LicenseStatusLabel.en"] = "Status",

        ["Admin.LicenseMessageLabel"]    = "Message",
        ["Admin.LicenseMessageLabel.fa"] = "پیام",
        ["Admin.LicenseMessageLabel.en"] = "Message",

        ["Admin.LicenseOwnerLabel"]    = "Owner",
        ["Admin.LicenseOwnerLabel.fa"] = "مالک",
        ["Admin.LicenseOwnerLabel.en"] = "Owner",

        ["Admin.LicenseCustomerLabel"]    = "Customer",
        ["Admin.LicenseCustomerLabel.fa"] = "مشتری",
        ["Admin.LicenseCustomerLabel.en"] = "Customer",

        ["Admin.LicenseEditionLabel"]    = "Edition",
        ["Admin.LicenseEditionLabel.fa"] = "نسخه",
        ["Admin.LicenseEditionLabel.en"] = "Edition",

        ["Admin.LicenseExpiryLabel"]    = "Expiry",
        ["Admin.LicenseExpiryLabel.fa"] = "انقضا",
        ["Admin.LicenseExpiryLabel.en"] = "Expiry",

        ["Admin.LicenseDaysLabel"]    = "Days remaining",
        ["Admin.LicenseDaysLabel.fa"] = "روزهای باقی‌مانده",
        ["Admin.LicenseDaysLabel.en"] = "Days remaining",

        ["Admin.LicenseUsersLabel"]    = "Users",
        ["Admin.LicenseUsersLabel.fa"] = "کاربران",
        ["Admin.LicenseUsersLabel.en"] = "Users",

        ["Admin.LicenseAdminsLabel"]    = "Admins",
        ["Admin.LicenseAdminsLabel.fa"] = "ادمین‌ها",
        ["Admin.LicenseAdminsLabel.en"] = "Admins",

        ["Admin.LicenseBotLabel"]    = "Bot",
        ["Admin.LicenseBotLabel.fa"] = "بات",
        ["Admin.LicenseBotLabel.en"] = "Bot",
    };

    private const string CacheKeyPrefix = "botsettings:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public BotTextService(IUnitOfWork uow, ICacheService cache)
    {
        _uow = uow;
        _cache = cache;
    }

    // ── Base lookup (no language suffix) ─────────────────────────────────────
    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        var cacheKey = CacheKeyPrefix + key;
        var cached = await _cache.GetAsync(cacheKey);
        if (cached is not null) return cached;

        var stored = await _uow.BotSettings.GetValueAsync(key);
        var value = stored ?? (Defaults.TryGetValue(key, out var def) ? def : defaultValue);

        await _cache.SetAsync(cacheKey, value, CacheDuration);
        return value;
    }

    // ── Language-aware lookup: tries {key}.{lang} then falls back to {key} ───
    public async Task<string> GetAsync(string key, string lang, string defaultValue = "")
    {
        if (!string.IsNullOrEmpty(lang))
        {
            var langKey = $"{key}.{lang}";
            var langCacheKey = CacheKeyPrefix + langKey;
            var langCached = await _cache.GetAsync(langCacheKey);
            if (langCached is not null) return langCached;

            var langStored = await _uow.BotSettings.GetValueAsync(langKey);
            if (langStored is not null)
            {
                await _cache.SetAsync(langCacheKey, langStored, CacheDuration);
                return langStored;
            }

            if (Defaults.TryGetValue(langKey, out var langDef))
            {
                await _cache.SetAsync(langCacheKey, langDef, CacheDuration);
                return langDef;
            }
        }

        // Fall back to base key
        return await GetAsync(key, defaultValue);
    }

    // ── Format helpers ────────────────────────────────────────────────────────
    public async Task<string> FormatAsync(string key, Dictionary<string, string> vars, string defaultValue = "")
    {
        var template = await GetAsync(key, defaultValue);
        return ApplyVars(template, vars);
    }

    public async Task<string> FormatAsync(string key, string lang, Dictionary<string, string> vars, string defaultValue = "")
    {
        var template = await GetAsync(key, lang, defaultValue);
        return ApplyVars(template, vars);
    }

    public async Task SetAsync(string key, string value)
    {
        await _uow.BotSettings.UpsertAsync(key, value);
        await _uow.SaveChangesAsync();
        await _cache.RemoveAsync(CacheKeyPrefix + key);
    }

    private static string ApplyVars(string template, Dictionary<string, string> vars)
    {
        foreach (var (k, v) in vars)
            template = template.Replace($"{{{k}}}", v);
        return template;
    }
}
