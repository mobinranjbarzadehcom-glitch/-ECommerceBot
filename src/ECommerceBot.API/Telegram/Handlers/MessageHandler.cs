using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.DTOs.Ticket;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Audit;
using ECommerceBot.API.Infrastructure.Licensing;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Infrastructure.RateLimit;
using ECommerceBot.API.Infrastructure.Security;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.Telegram.Keyboards;
using ECommerceBot.API.Telegram.Messages;
using ECommerceBot.API.Telegram.Services;
using ECommerceBot.API.Telegram.States;
using ECommerceBot.API.UnitOfWork;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ECommerceBot.API.Telegram.Handlers;

public class MessageHandler : IMessageHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IUserService _userService;
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly ITicketService _ticketService;
    private readonly IAdminService _adminService;
    private readonly ISettingService _settingService;
    private readonly ITelegramMessageService _msg;
    private readonly IKeyboardBuilder _kb;
    private readonly IBotTextService _texts;
    private readonly IConversationManager _conv;
    private readonly IRateLimitService _rateLimit;
    private readonly IAuditLogService _audit;
    private readonly ICouponService _couponService;
    private readonly IAffiliateService _affiliateService;
    private readonly ITenantContext _tenantContext;
    private readonly ILicenseService _licenseService;
    private readonly IServerFingerprintService _fingerprintService;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(
        IUnitOfWork uow,
        IUserService userService,
        IOrderService orderService,
        IPaymentService paymentService,
        ITicketService ticketService,
        IAdminService adminService,
        ISettingService settingService,
        ITelegramMessageService msg,
        IKeyboardBuilder kb,
        IBotTextService texts,
        IConversationManager conv,
        IRateLimitService rateLimit,
        IAuditLogService audit,
        ICouponService couponService,
        IAffiliateService affiliateService,
        ITenantContext tenantContext,
        ILicenseService licenseService,
        IServerFingerprintService fingerprintService,
        ILogger<MessageHandler> logger)
    {
        _uow = uow;
        _userService = userService;
        _orderService = orderService;
        _paymentService = paymentService;
        _ticketService = ticketService;
        _adminService = adminService;
        _settingService = settingService;
        _msg = msg;
        _kb = kb;
        _texts = texts;
        _conv = conv;
        _rateLimit = rateLimit;
        _audit = audit;
        _couponService = couponService;
        _affiliateService = affiliateService;
        _tenantContext = tenantContext;
        _licenseService = licenseService;
        _fingerprintService = fingerprintService;
        _logger = logger;
    }

    public async Task HandleAsync(Message message, TelegramUser? user, CancellationToken ct = default)
    {
        var chatId = message.Chat.Id;
        var text = message.Text?.Trim();

        // /start is always allowed — user may be null
        if (text?.StartsWith("/start") == true)
        {
            await HandleStartAsync(message, user, ct);
            return;
        }

        if (user is null)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Errors.PleaseStart", "fa", "Please send /start to begin."), ct: ct);
            return;
        }

        var lang = user.PreferredLanguage;

        if (user.IsBlocked)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Errors.Blocked", lang, "❌ You are blocked from using this bot."), ct: ct);
            return;
        }

        if (_rateLimit.IsRateLimited(user.TelegramId))
        {
            _logger.LogWarning("Rate limit hit for user {TelegramId} ({FirstName})", user.TelegramId, user.FirstName);
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Errors.RateLimited", lang, "⚠️ Too many messages. Please wait a moment before trying again."), ct: ct);
            return;
        }

        await _conv.UpdateActivityAsync(user, ct);

        if (user.CurrentState != ConversationState.None)
        {
            await HandleStateAsync(message, user, ct);
            return;
        }

        if (message.Photo is not null)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Errors.UseMenuForReceipt", lang, "Please use the menu to start an order before sending a receipt."), ct: ct);
            return;
        }

        if (text is null) return;

        if (text.StartsWith('/'))
        {
            await HandleCommandAsync(text, user, ct);
            return;
        }

        await HandleMenuButtonAsync(text, user, ct);
    }

    private async Task HandleStartAsync(Message message, TelegramUser? user, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var from = message.From!;

        var isNewUser = user is null;
        if (user is null)
        {
            var createResult = await _userService.GetOrCreateUserAsync(from.Id, from.FirstName, from.LastName, from.Username);
            if (!createResult.IsSuccess)
            {
                await _msg.SendHtmlAsync(chatId, createResult.ErrorMessage ?? "خطا در ثبت‌نام.", ct: ct);
                return;
            }
            user = await _uow.Users.GetByTelegramIdAsync(from.Id);
            if (user is null) return;
            _logger.LogInformation("New user registered: {TelegramId} @{Username}", from.Id, from.Username ?? "—");
        }

        // Handle referral code: /start ref_XXXX
        if (isNewUser && message.Text is not null)
        {
            var parts = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].StartsWith("ref_", StringComparison.OrdinalIgnoreCase))
            {
                var refCode = parts[1][4..]; // strip "ref_" prefix
                if (!string.IsNullOrWhiteSpace(refCode))
                    await _affiliateService.TrackReferralAsync(refCode, user.Id);
            }
        }

        user.ChatId = chatId;
        user.LastActivity = DateTime.UtcNow;
        user.CurrentState = ConversationState.None;
        user.TempData = null;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        var lang = user.PreferredLanguage;

        var welcome = await _texts.FormatAsync("WelcomeMessage", lang, new()
        {
            ["name"] = user.FirstName
        }, $"👋 <b>Welcome, {user.FirstName}!</b>");

        var kb = user.Role == UserRole.Admin
            ? await _kb.BuildAdminMenuAsync(lang)
            : await _kb.BuildMainMenuAsync(lang);

        await _msg.SendHtmlAsync(chatId, welcome, kb, ct);
    }

    private async Task HandleCommandAsync(string text, TelegramUser user, CancellationToken ct)
    {
        var cmd = text.Split(' ')[0].ToLower();
        switch (cmd)
        {
            case "/menu":   await ShowMenuAsync(user, ct); break;
            case "/wallet": await ShowWalletAsync(user, ct); break;
            case "/orders": await ShowOrdersAsync(user, ct); break;
            case "/help":   await ShowHelpAsync(user, ct); break;
            case "/ref":    await ShowAffiliateAsync(user, ct); break;
            default:        await ShowMenuAsync(user, ct); break;
        }
    }

    private async Task HandleMenuButtonAsync(string text, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;

        var products = await _texts.GetAsync("MainMenu.ProductsButton", lang, "🛒 Products");
        var wallet   = await _texts.GetAsync("MainMenu.WalletButton",   lang, "💰 Wallet");
        var orders   = await _texts.GetAsync("MainMenu.OrdersButton",   lang, "📦 Orders");
        var support  = await _texts.GetAsync("MainMenu.SupportButton",  lang, "🎫 Support");
        var help     = await _texts.GetAsync("MainMenu.HelpButton",     lang, "❓ Help");

        if (text == products) { await ShowProductCategoriesAsync(user, ct); return; }
        if (text == wallet)   { await ShowWalletAsync(user, ct); return; }
        if (text == orders)   { await ShowOrdersAsync(user, ct); return; }
        if (text == support)  { await ShowSupportAsync(user, ct); return; }
        if (text == help)     { await ShowHelpAsync(user, ct); return; }

        if (user.Role == UserRole.Admin)
        {
            await HandleAdminMenuButtonAsync(text, user, ct);
            return;
        }

        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.GetAsync("Errors.UseMenuButtons", lang, "Please use the menu buttons."),
            await _kb.BuildMainMenuAsync(lang), ct);
    }

    // ─── User flows ──────────────────────────────────────────────────────────

    private async Task ShowMenuAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var kb = user.Role == UserRole.Admin
            ? await _kb.BuildAdminMenuAsync(lang)
            : await _kb.BuildMainMenuAsync(lang);
        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.GetAsync("Menu.Title", lang, "📋 <b>Menu</b>"), kb, ct);
    }

    private async Task ShowProductCategoriesAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var categories = (await _uow.Categories.GetActiveCategoriesAsync()).ToList();
        if (categories.Count == 0)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Products.NoCategoriesAvailable", lang, "😔 No categories available at the moment."), ct: ct);
            return;
        }
        var kb = _kb.BuildCategoriesKeyboard(categories.Select(c => (c.Id, c.Name)));
        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.GetAsync("Products.SelectCategory", lang, "🛒 <b>Select a category:</b>"), kb, ct);
    }

    private async Task ShowWalletAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var transactions = (await _uow.WalletTransactions.GetByUserIdAsync(user.Id)).Take(5).ToList();

        var html = await _texts.FormatAsync("Wallet.Title", lang,
            new() { ["balance"] = $"{user.WalletBalance:F2}" },
            $"💰 <b>Wallet</b>\n\nBalance: <b>{user.WalletBalance:F2}$</b>");

        if (transactions.Any())
        {
            html += "\n\n" + await _texts.GetAsync("Wallet.RecentTransactions", lang, "📋 <b>Recent Transactions:</b>");
            foreach (var tx in transactions)
                html += $"\n• {tx.Type}: <b>{tx.Amount:+F2;-F2}$</b> — {tx.CreatedAt:MMM dd}";
        }

        await _msg.SendHtmlAsync(user.ChatId, html, ct: ct);
    }

    private async Task ShowOrdersAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var result = await _orderService.GetUserOrdersAsync(user.Id);
        var orders = result.Data?.Take(5).ToList() ?? new();

        if (orders.Count == 0)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Orders.Empty", lang, "📦 You have no orders yet."), ct: ct);
            return;
        }

        var html = await _texts.GetAsync("Orders.RecentTitle", lang, "📦 <b>Your Recent Orders:</b>") + "\n";
        foreach (var o in orders)
        {
            var icon = o.Status switch
            {
                OrderStatus.Completed => "✅",
                OrderStatus.Pending   => "⏳",
                OrderStatus.Cancelled => "❌",
                _                     => "❓"
            };
            html += $"\n{icon} <b>#{o.Id}</b> — {o.TotalAmount:F2}$ — {o.CreatedAt:MMM dd}";
        }

        await _msg.SendHtmlAsync(user.ChatId, html, ct: ct);
    }

    private async Task ShowSupportAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = await _texts.GetAsync("SupportWelcomeMessage", lang,
            "🎫 <b>Support</b>\n\nSend your message and we'll get back to you shortly.");
        await _conv.SetStateAsync(user, ConversationState.AwaitingTicketMessage, ct);
        await _msg.SendHtmlAsync(user.ChatId, text, await _kb.BuildCancelKeyboardAsync(lang), ct);
    }

    private async Task ShowHelpAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = await _texts.GetAsync("HelpMessage", lang, "📋 <b>Help</b>\n\nUse the menu to navigate.");
        await _msg.SendHtmlAsync(user.ChatId, text, ct: ct);
    }

    // ─── State machine ───────────────────────────────────────────────────────

    private async Task HandleStateAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var cancelBtn = await _texts.GetAsync("Buttons.CancelButton", lang, "❌ Cancel");
        if (message.Text == cancelBtn)
        {
            await _conv.ClearStateAsync(user, ct);
            await ShowMenuAsync(user, ct);
            return;
        }

        switch (user.CurrentState)
        {
            case ConversationState.AwaitingPlayerId:
                await HandlePlayerIdInputAsync(message, user, ct);
                break;
            case ConversationState.AwaitingReceipt:
                await HandleReceiptInputAsync(message, user, ct);
                break;
            case ConversationState.AwaitingTicketMessage:
                await HandleTicketMessageAsync(message, user, ct);
                break;
            case ConversationState.AwaitingRejectReason:
                await HandleAdminRejectReasonAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCategoryName:
                await HandleAdminCategoryNameAsync(message, user, ct);
                break;
            case ConversationState.AwaitingProductTitle:
                await HandleAdminProductTitleAsync(message, user, ct);
                break;
            case ConversationState.AwaitingProductPrice:
                await HandleAdminProductPriceAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCardNumber:
                await HandleAdminCardNumberAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCardHolder:
                await HandleAdminCardHolderAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCardBank:
                await HandleAdminCardBankAsync(message, user, ct);
                break;
            case ConversationState.AwaitingSettingValue:
                await HandleAdminSettingValueAsync(message, user, ct);
                break;
            case ConversationState.AwaitingAdminMessage:
                await HandleAdminMessageUserAsync(message, user, ct);
                break;
            case ConversationState.AwaitingLicenseKey:
                await HandleAdminLicenseKeyInputAsync(message, user, ct);
                break;
            case ConversationState.AwaitingProductDescription:
                await HandleAdminProductDescriptionAsync(message, user, ct);
                break;
            case ConversationState.AwaitingProductKeys:
                await HandleAdminProductKeysAsync(message, user, ct);
                break;
            case ConversationState.AwaitingNewAdminTelegramId:
                await HandleNewAdminTelegramIdAsync(message, user, ct);
                break;
            case ConversationState.AwaitingBackupChannelId:
                await HandleBackupChannelIdAsync(message, user, ct);
                break;
            case ConversationState.AwaitingApplyCoupon:
                await HandleApplyCouponAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCouponCode:
                await HandleAdminCouponCodeAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCouponDiscountValue:
                await HandleAdminCouponDiscountValueAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCouponMaxUses:
                await HandleAdminCouponMaxUsesAsync(message, user, ct);
                break;
            case ConversationState.AwaitingCouponExpiry:
                await HandleAdminCouponExpiryAsync(message, user, ct);
                break;
            default:
                await _conv.ClearStateAsync(user, ct);
                await ShowMenuAsync(user, ct);
                break;
        }
    }

    private async Task HandlePlayerIdInputAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var playerId = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Errors.PlayerIdEmpty", lang, "❌ Player ID cannot be empty. Please try again:"), ct: ct);
            return;
        }

        var ctx = await _conv.GetOrderContextAsync(user) ?? new OrderContext();
        ctx.PlayerId = playerId;
        await _conv.SetOrderContextAsync(user, ctx, ct);

        // Check if plan allows coupons before offering the coupon step
        var tenant = _tenantContext.IsSet
            ? await _uow.Tenants.GetByIdAsync(_tenantContext.TenantId)
            : null;
        var planAllowsCoupons = tenant?.Plan?.AllowsCoupons ?? false;

        if (planAllowsCoupons)
        {
            await _conv.SetStateAsync(user, ConversationState.AwaitingApplyCoupon, ct);
            await _msg.SendHtmlAsync(user.ChatId,
                $"✅ شناسه بازیکن ذخیره شد: <code>{HtmlSanitizer.Encode(playerId)}</code>\n\n" +
                "🎟 آیا کد تخفیف دارید؟\nکد خود را وارد کنید یا «⏭️ بدون تخفیف» را بزنید:",
                await _kb.BuildCouponOrSkipKeyboardAsync(lang), ct);
        }
        else
        {
            await _conv.SetStateAsync(user, ConversationState.AwaitingReceipt, ct);
            await ShowPaymentInstructionsAsync(user, ctx, ct);
        }
    }

    private async Task HandleReceiptInputAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        if (message.Photo is null)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Errors.ReceiptNotPhoto", lang, "📸 Please send the receipt as a photo."), ct: ct);
            return;
        }

        var photo = message.Photo[^1];
        var fileId = photo.FileId;
        var fileUniqueId = photo.FileUniqueId;

        var validateResult = await _paymentService.ValidateReceiptUniqueIdAsync(fileUniqueId);
        if (!validateResult.IsSuccess)
        {
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {validateResult.ErrorMessage}", ct: ct);
            return;
        }

        var ctx = await _conv.GetOrderContextAsync(user);
        if (ctx is null)
        {
            await _conv.ClearStateAsync(user, ct);
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Errors.SessionExpired", lang, "❌ Session expired. Please start over."), ct: ct);
            return;
        }

        var request = new CreateOrderRequest
        {
            ProductId = ctx.ProductId,
            Quantity = ctx.Quantity,
            PaymentMethod = PaymentMethod.CardPayment,
            ReceiptPhotoFileId = fileId,
            ReceiptPhotoUniqueId = fileUniqueId,
            AccountDetails = ctx.PlayerId,
            DiscountAmount = ctx.DiscountAmount,
            CouponCode = ctx.CouponCode
        };

        var result = await _orderService.CreateOrderAsync(user.Id, request);
        if (!result.IsSuccess)
        {
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {result.ErrorMessage}", ct: ct);
            return;
        }

        await _conv.ClearStateAsync(user, ct);
        var order = result.Data!;

        // Record coupon usage if a coupon was applied
        if (!string.IsNullOrEmpty(ctx.CouponCode))
        {
            var coupon = await _uow.Coupons.GetByCodeAsync(ctx.CouponCode);
            if (coupon is not null)
            {
                await _couponService.RecordUsageAsync(coupon.Id, user.Id, order.Id);
                await _audit.LogAsync(user.Id, AuditAction.ApplyCoupon, "Order", order.Id,
                    $"Coupon={ctx.CouponCode}, Discount={ctx.DiscountAmount}");
            }
        }

        var pendingMsg = await _texts.FormatAsync("OrderPendingMessage", lang,
            new() { ["orderId"] = order.Id.ToString() },
            $"⏳ <b>Order #{order.Id} submitted!</b> We'll review it shortly.");
        await _msg.SendHtmlAsync(user.ChatId, pendingMsg, await _kb.BuildMainMenuAsync(lang), ct);

        _logger.LogInformation("Order #{OrderId} created by user {TelegramId}", order.Id, user.TelegramId);

        var adminCaption = $"🆕 <b>سفارش جدید #{order.Id}</b>\n" +
                           $"👤 کاربر: {HtmlSanitizer.Encode(user.FirstName)} (@{HtmlSanitizer.Encode(user.Username ?? "—")})\n" +
                           $"📦 محصول: {HtmlSanitizer.Encode(ctx.ProductName)}\n" +
                           $"💰 مبلغ: {order.TotalAmount:F0} تومان" +
                           (ctx.DiscountAmount > 0 ? $" (تخفیف: {ctx.DiscountAmount:F0})" : "") + "\n" +
                           (ctx.CouponCode is not null ? $"🎟 کوپن: <code>{HtmlSanitizer.Encode(ctx.CouponCode)}</code>\n" : "") +
                           $"🎮 حساب: <code>{HtmlSanitizer.Encode(ctx.PlayerId ?? "—")}</code>\n" +
                           $"🕐 {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

        // Admin panels are always Persian (admin's default lang)
        var adminKb = await _kb.BuildOrderAdminActionsAsync(order.Id, "fa");
        await _msg.NotifyAdminsWithPhotoAsync(fileId, adminCaption, adminKb, ct);
        await NotifyDbAdminsAsync(fileId, adminCaption, adminKb, ct);
        await _msg.ForwardToBackupAsync(user.ChatId, message.MessageId, ct);
    }

    private async Task HandleTicketMessageAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Errors.TicketMessageEmpty", lang, "❌ Message cannot be empty."), ct: ct);
            return;
        }

        var subject = await _texts.FormatAsync("Ticket.SubjectFormat", lang,
            new() { ["name"] = user.FirstName }, $"Support from {user.FirstName}");

        var result = await _ticketService.CreateTicketAsync(user.Id, new CreateTicketDto
        {
            Subject = subject,
            Message = text
        });

        await _conv.ClearStateAsync(user, ct);

        if (result.IsSuccess)
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.FormatAsync("Ticket.CreatedSuccess", lang,
                    new() { ["ticketId"] = result.Data!.Id.ToString() },
                    $"✅ <b>Ticket #{result.Data!.Id} created!</b> We'll respond soon."),
                await _kb.BuildMainMenuAsync(lang), ct);
        else
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {result.ErrorMessage}", ct: ct);
    }

    // ─── Admin menu flows ────────────────────────────────────────────────────

    private async Task HandleAdminMenuButtonAsync(string text, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;

        var pendingOrders = await _texts.GetAsync("AdminMenu.OrdersButton",     lang, "📋 سفارش‌های در انتظار");
        var users         = await _texts.GetAsync("AdminMenu.UsersButton",      lang, "👥 کاربران");
        var products      = await _texts.GetAsync("AdminMenu.ProductsButton",   lang, "📦 محصولات");
        var categories    = await _texts.GetAsync("AdminMenu.CategoriesButton", lang, "🗂 دسته‌بندی‌ها");
        var cards         = await _texts.GetAsync("AdminMenu.CardsButton",      lang, "💳 کارت‌های بانکی");
        var settings      = await _texts.GetAsync("AdminMenu.SettingsButton",   lang, "⚙️ تنظیمات");
        var stats         = await _texts.GetAsync("AdminMenu.StatisticsButton", lang, "📊 آمار");
        var admins        = await _texts.GetAsync("AdminMenu.AdminsButton",     lang, "👑 مدیریت ادمین‌ها");
        var userView      = await _texts.GetAsync("AdminMenu.UserViewButton",   lang, "👁 مشاهده مثل کاربر");
        var license       = await _texts.GetAsync("AdminMenu.LicenseButton",    lang, "🔐 وضعیت لایسنس");
        var coupons       = await _texts.GetAsync("AdminMenu.CouponsButton",    lang, "🎟 کوپن‌ها");

        if (text == pendingOrders) { await ShowAdminPendingOrdersAsync(user, ct); return; }
        if (text == users)         { await ShowAdminUsersAsync(user, ct); return; }
        if (text == products)      { await ShowAdminProductsAsync(user, ct); return; }
        if (text == categories)    { await ShowAdminCategoriesAsync(user, ct); return; }
        if (text == cards)         { await ShowAdminCardsAsync(user, ct); return; }
        if (text == settings)      { await ShowAdminSettingsAsync(user, ct); return; }
        if (text == stats)         { await ShowAdminStatsAsync(user, ct); return; }
        if (text == admins)        { await ShowAdminManagementAsync(user, ct); return; }
        if (text == userView)      { await ShowUserViewAsync(user, ct); return; }
        if (text == license)       { await ShowAdminLicenseStatusAsync(user, ct); return; }
        if (text == coupons)       { await ShowAdminCouponsAsync(user, ct); return; }

        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.GetAsync("Admin.UseMenu", lang, "لطفاً از دکمه‌های منو استفاده کنید."),
            await _kb.BuildAdminMenuAsync(lang), ct);
    }

    private async Task ShowAdminPendingOrdersAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var result = await _orderService.GetPendingOrdersAsync(1, 10);
        var orders = result.Data?.Items.ToList() ?? new();

        if (orders.Count == 0)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.NoPendingOrders", lang, "✅ No pending orders."), ct: ct);
            return;
        }

        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.FormatAsync("Admin.PendingOrdersTitle", lang,
                new() { ["count"] = orders.Count.ToString() },
                $"📋 <b>{orders.Count} Pending Orders:</b>"), ct: ct);

        foreach (var o in orders)
        {
            var html = $"🆔 Order <b>#{o.Id}</b>\n" +
                       $"👤 User: {o.UserName}\n" +
                       $"💰 Amount: <b>{o.TotalAmount:F2}$</b>\n" +
                       $"🎮 Account: <code>{o.AccountDetails ?? "—"}</code>\n" +
                       $"🕐 {o.CreatedAt:yyyy-MM-dd HH:mm}";

            var kb = await _kb.BuildOrderAdminActionsAsync(o.Id, lang);

            if (!string.IsNullOrEmpty(o.ReceiptPhotoFileId))
                await _msg.SendPhotoAsync(user.ChatId, o.ReceiptPhotoFileId, html, kb, ct);
            else
                await _msg.SendHtmlAsync(user.ChatId, html, kb, ct);
        }
    }

    private async Task ShowAdminUsersAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var result = await _userService.GetAllUsersAsync(1, 10);
        var users = result.Data?.Items.ToList() ?? new();

        var html = await _texts.FormatAsync("Admin.UsersTitle", lang,
            new() { ["count"] = (result.Data?.TotalCount ?? 0).ToString() },
            $"👥 <b>Users ({result.Data?.TotalCount ?? 0} total)</b>") + "\n";

        foreach (var u in users.Take(10))
        {
            var icon = u.IsBlocked ? "🚫" : (u.Role == UserRole.Admin ? "👑" : "👤");
            html += $"\n{icon} <b>{u.FirstName}</b> (@{u.Username}) — 💰{u.WalletBalance:F2}$";
        }
        await _msg.SendHtmlAsync(user.ChatId, html, ct: ct);
    }

    private async Task ShowAdminProductsAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var products = (await _uow.Products.GetActiveProductsAsync()).ToList();
        if (products.Count == 0)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.NoProductsFound", lang, "No products found."), ct: ct);
            return;
        }

        var html = await _texts.GetAsync("Admin.ProductsTitle", lang, "📦 <b>Products:</b>");
        var addLabel = await _texts.GetAsync("Admin.AddProduct", lang, "➕ Add Product");
        var rows = products.Select(p =>
            new[] { InlineKeyboardButton.WithCallbackData($"#{p.Id} {p.Name} — {p.Price:F0}$", $"adm:prod:{p.Id}") }
        ).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(addLabel, "adm:prod:add") });
        await _msg.SendHtmlAsync(user.ChatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    private async Task ShowAdminCategoriesAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var cats = (await _uow.Categories.GetAllAsync()).ToList();
        var html = await _texts.GetAsync("Admin.CategoriesTitle", lang, "🗂 <b>Categories:</b>");
        var addLabel = await _texts.GetAsync("Admin.AddCategory", lang, "➕ Add Category");
        var rows = cats.Select(c =>
            new[] { InlineKeyboardButton.WithCallbackData($"{(c.IsActive ? "✅" : "❌")} {c.Name}", $"adm:cat:{c.Id}") }
        ).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(addLabel, "adm:cat:add") });
        await _msg.SendHtmlAsync(user.ChatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    private async Task ShowAdminCardsAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var cards = (await _uow.PaymentCards.GetActiveCardsAsync()).ToList();
        var html = await _texts.GetAsync("Admin.CardsTitle", lang, "💳 <b>Payment Cards:</b>");
        var addLabel = await _texts.GetAsync("Admin.AddCard", lang, "➕ Add Card");
        var rows = cards.Select(c =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"{(c.IsDefault ? "⭐" : "")} {c.CardNumber} ({c.BankName})", $"adm:card:{c.Id}") }
        ).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(addLabel, "adm:card:add") });
        await _msg.SendHtmlAsync(user.ChatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    private async Task ShowAdminSettingsAsync(TelegramUser user, CancellationToken ct)
    {
        await _msg.SendHtmlAsync(user.ChatId,
            "⚙️ <b>تنظیمات ربات</b>\n\nدسته‌بندی مورد نظر را انتخاب کنید:",
            _kb.BuildSettingsCategoriesKeyboard(), ct);
    }

    private async Task ShowAdminStatsAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var totalOrders     = await _uow.Orders.CountAsync();
        var pendingOrders   = await _uow.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
        var completedOrders = await _uow.Orders.CountAsync(o => o.Status == OrderStatus.Completed);
        var totalUsers      = await _uow.Users.CountAsync();
        var blockedUsers    = await _uow.Users.CountAsync(u => u.IsBlocked);

        var title = await _texts.GetAsync("Admin.StatsTitle", lang, "📊 <b>Statistics</b>");
        var html = $"{title}\n\n" +
                   $"📦 Orders: <b>{totalOrders}</b> (⏳{pendingOrders} pending, ✅{completedOrders} done)\n" +
                   $"👥 Users: <b>{totalUsers}</b> (🚫{blockedUsers} blocked)";

        await _msg.SendHtmlAsync(user.ChatId, html, ct: ct);
    }

    // ─── Admin state handlers ────────────────────────────────────────────────

    private async Task HandleAdminRejectReasonAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var reason = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.ReasonEmpty", lang, "❌ Reason cannot be empty."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        if (ctx?.TargetOrderId is null) { await _conv.ClearStateAsync(user, ct); return; }

        var result = await _adminService.RejectOrderAsync(ctx.TargetOrderId.Value, user.Id, reason);
        await _conv.ClearStateAsync(user, ct);

        if (result.IsSuccess)
        {
            var confirm = await _texts.FormatAsync("Admin.OrderRejected", lang,
                new() { ["orderId"] = ctx.TargetOrderId.Value.ToString() },
                $"✅ Order #{ctx.TargetOrderId} rejected.");
            await _msg.SendHtmlAsync(user.ChatId, confirm, await _kb.BuildAdminMenuAsync(lang), ct);
            await NotifyOrderUserAsync(ctx.TargetOrderId.Value, "OrderRejectedMessage",
                new() { ["orderId"] = ctx.TargetOrderId.Value.ToString(), ["reason"] = reason }, ct);
        }
        else
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {result.ErrorMessage}", ct: ct);
    }

    private async Task HandleAdminCategoryNameAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var name = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.NameEmpty", lang, "❌ Name cannot be empty."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        await _conv.ClearStateAsync(user, ct);

        if (ctx?.TargetCategoryId.HasValue == true)
        {
            var cat = await _uow.Categories.GetByIdAsync(ctx.TargetCategoryId.Value);
            if (cat != null) { cat.Name = name; _uow.Categories.Update(cat); await _uow.SaveChangesAsync(ct); }
            await _audit.LogAsync(user.Id, AuditAction.EditCategory, "Category", ctx.TargetCategoryId, $"Renamed to: {name}");
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.FormatAsync("Admin.CategoryRenamed", lang,
                    new() { ["name"] = HtmlSanitizer.Encode(name) },
                    $"✅ Category renamed to <b>{HtmlSanitizer.Encode(name)}</b>."),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
        else
        {
            await _uow.Categories.AddAsync(new Category { Name = name, IsActive = true });
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(user.Id, AuditAction.AddCategory, "Category", null, name);
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.FormatAsync("Admin.CategoryCreated", lang,
                    new() { ["name"] = HtmlSanitizer.Encode(name) },
                    $"✅ Category <b>{HtmlSanitizer.Encode(name)}</b> created."),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
    }

    private async Task HandleAdminProductTitleAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var title = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.TitleEmpty", lang, "❌ عنوان نمی‌تواند خالی باشد."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();

        if (ctx.TargetProductId.HasValue)
        {
            // Edit existing product name
            await _conv.ClearStateAsync(user, ct);
            var prod = await _uow.Products.GetByIdAsync(ctx.TargetProductId.Value);
            if (prod != null) { prod.Name = title; _uow.Products.Update(prod); await _uow.SaveChangesAsync(ct); }
            await _audit.LogAsync(user.Id, AuditAction.EditProductTitle, "Product", ctx.TargetProductId, $"Title: {title}");
            await _msg.SendHtmlAsync(user.ChatId,
                $"✅ نام محصول به <b>{HtmlSanitizer.Encode(title)}</b> تغییر یافت.",
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
        else if (ctx.PendingAction == "new_product")
        {
            // Creation wizard → next step: description
            ctx.PendingProductTitle = title;
            await _conv.SetAdminContextAsync(user, ctx, ct);
            await _conv.SetStateAsync(user, ConversationState.AwaitingProductDescription, ct);
            await _msg.SendHtmlAsync(user.ChatId,
                $"✅ عنوان: <b>{HtmlSanitizer.Encode(title)}</b>\n\n" +
                "📝 توضیحات محصول را وارد کنید:\n<i>(برای رد شدن، دکمه «⏭️ رد شدن» را بزنید)</i>",
                await _kb.BuildSkipCancelKeyboardAsync(lang), ct);
        }
    }

    private async Task HandleAdminProductPriceAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        if (!decimal.TryParse(message.Text?.Trim(), out var price) || price <= 0)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.InvalidPrice", lang, "❌ Invalid price. Enter a positive number."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        await _conv.ClearStateAsync(user, ct);

        if (ctx?.TargetProductId.HasValue == true)
        {
            var prod = await _uow.Products.GetByIdAsync(ctx.TargetProductId.Value);
            if (prod != null) { prod.Price = price; _uow.Products.Update(prod); await _uow.SaveChangesAsync(ct); }
            await _audit.LogAsync(user.Id, AuditAction.EditProductPrice, "Product", ctx.TargetProductId, $"Price: {price:F2}");
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.FormatAsync("Admin.ProductPriceSet", lang,
                    new() { ["price"] = price.ToString("F2") },
                    $"✅ Product price set to <b>{price:F2}$</b>."),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
    }

    private async Task HandleAdminCardNumberAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var cardNumber = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.InvalidCardNumber", lang, "❌ Invalid card number."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();

        if (ctx.TargetCardId.HasValue)
        {
            var card = await _uow.PaymentCards.GetByIdAsync(ctx.TargetCardId.Value);
            if (card != null) { card.CardNumber = cardNumber; _uow.PaymentCards.Update(card); await _uow.SaveChangesAsync(ct); }
            await _conv.ClearStateAsync(user, ct);
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.CardNumberUpdated", lang, "✅ Card number updated."),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
        else
        {
            ctx.PendingAction = cardNumber;
            await _conv.SetAdminContextAsync(user, ctx, ct);
            await _conv.SetStateAsync(user, ConversationState.AwaitingCardHolder, ct);
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.CardHolderPrompt", lang, "👤 Enter cardholder name:"), ct: ct);
        }
    }

    private async Task HandleAdminCardHolderAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var holder = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(holder))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.InvalidName", lang, "❌ Invalid name."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();
        var cardNumber = ctx.PendingAction ?? string.Empty;
        ctx.PendingAction = $"{cardNumber}|{holder}";
        await _conv.SetAdminContextAsync(user, ctx, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingCardBank, ct);
        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.GetAsync("Admin.BankPrompt", lang, "🏦 Enter bank name:"), ct: ct);
    }

    private async Task HandleAdminCardBankAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var bank = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(bank))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.InvalidBankName", lang, "❌ Invalid bank name."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        var parts = ctx?.PendingAction?.Split('|') ?? Array.Empty<string>();
        await _conv.ClearStateAsync(user, ct);

        if (parts.Length >= 2)
        {
            await _uow.PaymentCards.AddAsync(new PaymentCard
            {
                CardNumber = parts[0],
                CardHolderName = parts[1],
                BankName = bank,
                IsActive = true
            });
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(user.Id, AuditAction.AddCard, "PaymentCard", null, $"Card: {parts[0]}");
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.CardAdded", lang, "✅ Payment card added."),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
    }

    private async Task HandleAdminSettingValueAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var value = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.ValueEmpty", lang, "❌ Value cannot be empty."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        await _conv.ClearStateAsync(user, ct);

        if (ctx?.TargetSettingKey is not null)
        {
            await _texts.SetAsync(ctx.TargetSettingKey, value);
            await _audit.LogAsync(user.Id, AuditAction.EditSetting, "BotSetting", null, $"Key={ctx.TargetSettingKey}");
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.FormatAsync("Admin.SettingUpdated", lang,
                    new() { ["key"] = HtmlSanitizer.Encode(ctx.TargetSettingKey) },
                    $"✅ Setting <b>{HtmlSanitizer.Encode(ctx.TargetSettingKey)}</b> updated."),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
    }

    private async Task HandleAdminMessageUserAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.Errors.MessageEmpty", lang, "❌ Message cannot be empty."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        await _conv.ClearStateAsync(user, ct);

        if (ctx?.TargetUserId.HasValue == true)
        {
            var targetUser = await _uow.Users.GetByIdAsync(ctx.TargetUserId.Value);
            if (targetUser?.ChatId > 0)
            {
                var msgText = await _texts.FormatAsync("Admin.AdminMessageToUser", targetUser.PreferredLanguage,
                    new() { ["text"] = HtmlSanitizer.Encode(text) },
                    $"📨 <b>Message from Admin:</b>\n\n{HtmlSanitizer.Encode(text)}");
                await _msg.SendHtmlAsync(targetUser.ChatId, msgText, ct: ct);
                await _audit.LogAsync(user.Id, AuditAction.MessageUser, "User", ctx.TargetUserId, "Message sent");
                await _msg.SendHtmlAsync(user.ChatId,
                    await _texts.GetAsync("Admin.MessageSent", lang, "✅ Message sent."),
                    await _kb.BuildAdminMenuAsync(lang), ct);
            }
        }
    }

    private async Task ShowAdminLicenseStatusAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var result = _licenseService.GetCachedResult();
        var statusIcon = result.IsValid ? "✅" : "❌";

        var expiresDisplay = result.ExpiresAt.HasValue
            ? result.ExpiresAt.Value.ToString("yyyy-MM-dd")
            : await _texts.GetAsync("Admin.LicenseNoExpiry", lang, "♾ No expiration");

        var title          = await _texts.GetAsync("Admin.LicenseStatusTitle",    lang, "🔐 <b>License Status</b>");
        var statusLabel    = await _texts.GetAsync("Admin.LicenseStatusLabel",    lang, "Status");
        var messageLabel   = await _texts.GetAsync("Admin.LicenseMessageLabel",   lang, "Message");
        var ownerLabel     = await _texts.GetAsync("Admin.LicenseOwnerLabel",     lang, "Owner");
        var customerLabel  = await _texts.GetAsync("Admin.LicenseCustomerLabel",  lang, "Customer");
        var editionLabel   = await _texts.GetAsync("Admin.LicenseEditionLabel",   lang, "Edition");
        var expiryLabel    = await _texts.GetAsync("Admin.LicenseExpiryLabel",    lang, "Expiry");
        var daysLabel      = await _texts.GetAsync("Admin.LicenseDaysLabel",      lang, "Days remaining");
        var usersLabel     = await _texts.GetAsync("Admin.LicenseUsersLabel",     lang, "Users");
        var adminsLabel    = await _texts.GetAsync("Admin.LicenseAdminsLabel",    lang, "Admins");
        var botLabel       = await _texts.GetAsync("Admin.LicenseBotLabel",       lang, "Bot");
        var trialLabel     = await _texts.GetAsync("Admin.LicenseTrial",          lang, "⚠️ <b>Trial License</b>");

        var html = $"{title}\n\n" +
                   $"{statusIcon} {statusLabel}: <b>{result.Status}</b>\n" +
                   $"📝 {messageLabel}: {HtmlSanitizer.Encode(result.Message)}\n";

        if (result.OwnerName is not null)    html += $"👤 {ownerLabel}: {HtmlSanitizer.Encode(result.OwnerName)}\n";
        if (result.CustomerName is not null) html += $"🏢 {customerLabel}: {HtmlSanitizer.Encode(result.CustomerName)}\n";
        if (result.Edition is not null)      html += $"📦 {editionLabel}: {HtmlSanitizer.Encode(result.Edition)}\n";

        html += $"⏰ {expiryLabel}: {expiresDisplay}\n";

        if (result.ExpiresAt.HasValue)   html += $"📅 {daysLabel}: <b>{result.DaysRemaining}</b>\n";
        if (result.MaxUsers > 0)         html += $"👥 {usersLabel}: {result.CurrentUsers}/{result.MaxUsers}\n";
        if (result.MaxAdmins > 0)        html += $"👑 {adminsLabel}: {result.CurrentAdmins}/{result.MaxAdmins}\n";
        if (result.BotUsername is not null) html += $"🤖 {botLabel}: @{HtmlSanitizer.Encode(result.BotUsername)}\n";
        if (result.IsTrial)              html += $"\n{trialLabel}";

        var kb = await _kb.BuildLicenseActionsKeyboardAsync(lang);
        await _msg.SendHtmlAsync(user.ChatId, html, kb, ct);
    }

    private async Task HandleAdminLicenseKeyInputAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var input = message.Text?.Trim();
        await _conv.ClearStateAsync(user, ct);

        if (string.IsNullOrWhiteSpace(input))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Admin.LicenseKeyEmpty", lang, "❌ License key is empty."),
                await _kb.BuildAdminMenuAsync(lang), ct);
            return;
        }

        var result = await _licenseService.ActivateAsync(input, ct);

        if (result.IsValid)
        {
            var success = await _texts.FormatAsync("License.ActivationSuccessMessage", lang, new()
            {
                ["edition"]   = result.Edition ?? "Standard",
                ["owner"]     = result.OwnerName ?? "—",
                ["expiresAt"] = result.ExpiresAt?.ToString("yyyy-MM-dd") ?? "♾"
            }, $"✅ License activated!\nEdition: {result.Edition}\nOwner: {result.OwnerName}");

            await _audit.LogAsync(user.Id, AuditAction.LicenseActivated, "License", null,
                $"Edition={result.Edition}, Owner={result.OwnerName}");
            await _msg.SendHtmlAsync(user.ChatId, success, await _kb.BuildAdminMenuAsync(lang), ct);
        }
        else
        {
            var failed = await _texts.FormatAsync("License.ActivationFailedMessage", lang,
                new() { ["error"] = result.Message },
                $"❌ License activation failed.\nError: {result.Message}");
            await _msg.SendHtmlAsync(user.ChatId, failed, await _kb.BuildAdminMenuAsync(lang), ct);
        }
    }

    // ─── Phase 3: product wizard, admin management, user-view ────────────────

    private async Task HandleAdminProductDescriptionAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = message.Text?.Trim();
        var ctx  = await _conv.GetAdminContextAsync(user) ?? new AdminContext();

        // "⏭️ رد شدن" → skip description
        ctx.PendingProductDescription = text == "⏭️ رد شدن" ? null : text;
        await _conv.SetAdminContextAsync(user, ctx, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingProductPrice, ct);

        await _msg.SendHtmlAsync(user.ChatId,
            "💰 قیمت محصول را وارد کنید (عدد):",
            await _kb.BuildCancelKeyboardAsync(lang), ct);
    }

    private async Task HandleAdminProductKeysAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var rawText = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            await _msg.SendHtmlAsync(user.ChatId, "❌ متنی دریافت نشد.", ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        await _conv.ClearStateAsync(user, ct);

        if (ctx?.TargetProductId is null) return;

        var lines = rawText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .ToList();

        if (lines.Count == 0)
        {
            await _msg.SendHtmlAsync(user.ChatId, "❌ هیچ کلیدی یافت نشد.", ct: ct);
            return;
        }

        var newKeys = lines.Select(k => new ProductKey
        {
            ProductId = ctx.TargetProductId.Value,
            KeyValue  = k,
            IsUsed    = false
        }).ToList();

        await _uow.ProductKeys.AddRangeAsync(newKeys);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(user.Id, AuditAction.AddProduct, "ProductKey", ctx.TargetProductId,
            $"{newKeys.Count} کلید اضافه شد");

        await _msg.SendHtmlAsync(user.ChatId,
            $"✅ <b>{newKeys.Count}</b> کلید برای محصول ثبت شد.",
            await _kb.BuildAdminMenuAsync(lang), ct);
    }

    private async Task ShowAdminManagementAsync(TelegramUser user, CancellationToken ct)
    {
        var admins = (await _uow.Users.GetAdminsAsync()).ToList();
        var html   = $"👑 <b>مدیریت ادمین‌ها</b>\n\nتعداد فعلی: <b>{admins.Count}</b>\n";
        var rows   = new List<InlineKeyboardButton[]>();

        foreach (var a in admins)
        {
            var label = $"👤 {a.FirstName}";
            if (!string.IsNullOrEmpty(a.Username)) label += $" (@{a.Username})";
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(label, $"adm:mgr:{a.Id}") });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ افزودن ادمین", "adm:mgr:add") });
        await _msg.SendHtmlAsync(user.ChatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    private async Task HandleNewAdminTelegramIdAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang  = user.PreferredLanguage;
        var input = message.Text?.Trim();
        await _conv.ClearStateAsync(user, ct);

        if (!long.TryParse(input, out var telegramId))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                "❌ شناسه تلگرام باید یک عدد باشد.", await _kb.BuildAdminMenuAsync(lang), ct);
            return;
        }

        var target = await _uow.Users.GetByTelegramIdAsync(telegramId);
        if (target is null)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                $"❌ کاربری با شناسه <code>{telegramId}</code> در این ربات یافت نشد.\n" +
                "ابتدا باید یک بار /start را برای ربات ارسال کرده باشد.",
                await _kb.BuildAdminMenuAsync(lang), ct);
            return;
        }

        if (target.Role == UserRole.Admin)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                $"ℹ️ کاربر <b>{HtmlSanitizer.Encode(target.FirstName)}</b> از قبل ادمین است.",
                await _kb.BuildAdminMenuAsync(lang), ct);
            return;
        }

        // Enforce MaxAdmins plan limit
        if (_tenantContext.IsSet)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(_tenantContext.TenantId);
            if (tenant is not null)
            {
                var currentAdmins = await _uow.Users.CountAsync(u => u.Role == UserRole.Admin);
                if (currentAdmins >= tenant.MaxAdmins)
                {
                    await _msg.SendHtmlAsync(user.ChatId,
                        $"❌ سقف ادمین‌های این پلن ({tenant.MaxAdmins} نفر) تکمیل شده است.",
                        await _kb.BuildAdminMenuAsync(lang), ct);
                    return;
                }
            }
        }

        target.Role = UserRole.Admin;
        _uow.Users.Update(target);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(user.Id, AuditAction.EditUser, "TelegramUser", target.Id, "Role set to Admin");

        await _msg.SendHtmlAsync(user.ChatId,
            $"✅ کاربر <b>{HtmlSanitizer.Encode(target.FirstName)}</b> به ادمین ارتقا یافت.",
            await _kb.BuildAdminMenuAsync(lang), ct);

        if (target.ChatId > 0)
            await _msg.SendHtmlAsync(target.ChatId,
                "👑 شما به عنوان ادمین این ربات تنظیم شدید. برای مشاهده منوی ادمین /start بزنید.", ct: ct);
    }

    private async Task HandleBackupChannelIdAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang  = user.PreferredLanguage;
        var input = message.Text?.Trim();
        await _conv.ClearStateAsync(user, ct);

        if (!long.TryParse(input, out _))
        {
            await _msg.SendHtmlAsync(user.ChatId,
                "❌ شناسه کانال باید یک عدد باشد (مثلاً -100123456789).",
                await _kb.BuildAdminMenuAsync(lang), ct);
            return;
        }

        await _texts.SetAsync("BackupChannelId", input!);
        await _msg.SendHtmlAsync(user.ChatId,
            $"✅ شناسه کانال بکاپ تنظیم شد: <code>{HtmlSanitizer.Encode(input!)}</code>",
            await _kb.BuildAdminMenuAsync(lang), ct);
    }

    private async Task ShowUserViewAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var kb   = await _kb.BuildMainMenuAsync(lang);
        await _msg.SendHtmlAsync(user.ChatId,
            "👁 <b>مشاهده مثل کاربر</b>\n\nمنوی زیر همان چیزی است که کاربران می‌بینند.\n" +
            "برای بازگشت به پنل ادمین، /start بزنید.",
            kb, ct);
    }

    // ─── Phase 4: Coupons + Affiliate ───────────────────────────────────────

    private async Task ShowAdminCouponsAsync(TelegramUser user, CancellationToken ct)
    {
        var result = await _couponService.GetAllAsync();
        var coupons = result.Data?.ToList() ?? new();
        var html = "🎟 <b>کوپن‌های تخفیف</b>\n\n";
        if (coupons.Count == 0)
            html += "هنوز کوپنی ثبت نشده است.";

        var rows = coupons.Select(c =>
        {
            var icon = c.IsActive ? "✅" : "❌";
            var label = c.DiscountType == DiscountType.Percentage
                ? $"{icon} {c.Code} — {c.DiscountValue:F0}%"
                : $"{icon} {c.Code} — {c.DiscountValue:F0} تومان";
            return new[] { InlineKeyboardButton.WithCallbackData(label, $"adm:cpn:{c.Id}") };
        }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ کوپن جدید", "adm:cpn:new") });

        await _msg.SendHtmlAsync(user.ChatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    // Coupon wizard: Step 1 — code
    private async Task HandleAdminCouponCodeAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var code = message.Text?.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3)
        {
            await _msg.SendHtmlAsync(user.ChatId, "❌ کد باید حداقل ۳ کاراکتر باشد.", ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();
        ctx.PendingCouponCode = code;
        await _conv.SetAdminContextAsync(user, ctx, ct);

        // Transition: pick discount type via inline keyboard (state stays AwaitingCouponCode until inline callback picks type)
        await _conv.SetStateAsync(user, ConversationState.AwaitingCouponDiscountValue, ct);
        await _msg.SendHtmlAsync(user.ChatId,
            $"✅ کد: <b>{HtmlSanitizer.Encode(code)}</b>\n\n📊 نوع تخفیف را انتخاب کنید:",
            _kb.BuildCouponDiscountTypeKeyboard(), ct);
    }

    // Coupon wizard: Step 2b — discount value (after type chosen via callback)
    private async Task HandleAdminCouponDiscountValueAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        if (!decimal.TryParse(message.Text?.Trim(), out var value) || value <= 0)
        {
            await _msg.SendHtmlAsync(user.ChatId, "❌ مقدار نامعتبر. یک عدد مثبت وارد کنید.", ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();
        if (ctx.PendingCouponDiscountType == "pct" && value > 100)
        {
            await _msg.SendHtmlAsync(user.ChatId, "❌ درصد تخفیف نمی‌تواند بیشتر از ۱۰۰ باشد.", ct: ct);
            return;
        }

        ctx.PendingCouponDiscountValue = value;
        await _conv.SetAdminContextAsync(user, ctx, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingCouponMaxUses, ct);

        await _msg.SendHtmlAsync(user.ChatId,
            "🔢 حداکثر تعداد استفاده را وارد کنید:\n<i>(برای نامحدود، «⏭️ رد شدن» را بزنید)</i>",
            await _kb.BuildSkipCancelKeyboardAsync(lang), ct);
    }

    // Coupon wizard: Step 3 — max uses
    private async Task HandleAdminCouponMaxUsesAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = message.Text?.Trim();
        int? maxUses = null;

        if (text != "⏭️ رد شدن")
        {
            if (!int.TryParse(text, out var n) || n <= 0)
            {
                await _msg.SendHtmlAsync(user.ChatId, "❌ عدد صحیح مثبت وارد کنید.", ct: ct);
                return;
            }
            maxUses = n;
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();
        ctx.PendingCouponMaxUses = maxUses;
        await _conv.SetAdminContextAsync(user, ctx, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingCouponExpiry, ct);

        await _msg.SendHtmlAsync(user.ChatId,
            "📅 تاریخ انقضا را وارد کنید (مثلاً 2026-12-31):\n<i>(برای بدون انقضا، «⏭️ رد شدن» را بزنید)</i>",
            await _kb.BuildSkipCancelKeyboardAsync(lang), ct);
    }

    // Coupon wizard: Step 4 — expiry → create
    private async Task HandleAdminCouponExpiryAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = message.Text?.Trim();
        DateTime? expiresAt = null;

        if (text != "⏭️ رد شدن")
        {
            if (!DateTime.TryParse(text, out var dt))
            {
                await _msg.SendHtmlAsync(user.ChatId, "❌ فرمت تاریخ نامعتبر است. مثلاً: 2026-12-31", ct: ct);
                return;
            }
            expiresAt = DateTime.SpecifyKind(dt.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
        }

        var ctx = await _conv.GetAdminContextAsync(user) ?? new AdminContext();
        await _conv.ClearStateAsync(user, ct);

        if (ctx.PendingCouponCode is null || ctx.PendingCouponDiscountType is null || ctx.PendingCouponDiscountValue is null)
        {
            await _msg.SendHtmlAsync(user.ChatId, "❌ اطلاعات کوپن ناقص است. دوباره تلاش کنید.",
                await _kb.BuildAdminMenuAsync(lang), ct);
            return;
        }

        var discountType = ctx.PendingCouponDiscountType == "pct"
            ? DiscountType.Percentage
            : DiscountType.Fixed;

        var result = await _couponService.CreateAsync(
            ctx.PendingCouponCode, discountType, ctx.PendingCouponDiscountValue.Value,
            null, ctx.PendingCouponMaxUses, expiresAt);

        if (result.IsSuccess)
        {
            var coupon = result.Data!;
            var typeLabel = discountType == DiscountType.Percentage ? "درصدی" : "مبلغ ثابت";
            await _audit.LogAsync(user.Id, AuditAction.CreateCoupon, "Coupon", coupon.Id, coupon.Code);
            await _msg.SendHtmlAsync(user.ChatId,
                $"✅ <b>کوپن ایجاد شد!</b>\n\n" +
                $"🔖 کد: <code>{HtmlSanitizer.Encode(coupon.Code)}</code>\n" +
                $"📊 نوع: {typeLabel}\n" +
                $"💰 مقدار: {coupon.DiscountValue:F0}" + (discountType == DiscountType.Percentage ? "%" : " تومان") + "\n" +
                (coupon.MaxUses.HasValue ? $"🔢 حداکثر استفاده: {coupon.MaxUses}\n" : "🔢 نامحدود\n") +
                (coupon.ExpiresAt.HasValue ? $"📅 انقضا: {coupon.ExpiresAt.Value:yyyy-MM-dd}" : "📅 بدون انقضا"),
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
        else
        {
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {result.ErrorMessage}",
                await _kb.BuildAdminMenuAsync(lang), ct);
        }
    }

    // User checkout: apply coupon
    private async Task HandleApplyCouponAsync(Message message, TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var text = message.Text?.Trim();

        if (text == "⏭️ بدون تخفیف" || text == "🎟 کد تخفیف دارم")
        {
            if (text == "🎟 کد تخفیف دارم")
            {
                await _msg.SendHtmlAsync(user.ChatId,
                    "🎟 کد تخفیف خود را وارد کنید:",
                    await _kb.BuildCancelKeyboardAsync(lang), ct);
                return;
            }
            // Skip coupon → go to receipt step
            await _conv.SetStateAsync(user, ConversationState.AwaitingReceipt, ct);
            var ctx = await _conv.GetOrderContextAsync(user);
            if (ctx is not null) await ShowPaymentInstructionsAsync(user, ctx, ct);
            return;
        }

        // Treat input as coupon code
        var couponCode = text ?? string.Empty;
        var orderCtx = await _conv.GetOrderContextAsync(user);
        if (orderCtx is null)
        {
            await _conv.ClearStateAsync(user, ct);
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.GetAsync("Errors.SessionExpired", lang, "❌ نشست منقضی شد. دوباره شروع کنید."), ct: ct);
            return;
        }

        var orderAmount = orderCtx.ProductPrice * orderCtx.Quantity;
        var validation = await _couponService.ValidateAsync(couponCode, user.Id, orderAmount);
        if (!validation.IsSuccess)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                $"❌ {validation.ErrorMessage}\n\nدوباره کد وارد کنید یا «⏭️ بدون تخفیف» را بزنید:",
                await _kb.BuildCouponOrSkipKeyboardAsync(lang), ct);
            return;
        }

        var vr = validation.Data!;
        orderCtx.CouponCode = vr.Coupon.Code;
        orderCtx.DiscountAmount = vr.DiscountAmount;
        await _conv.SetOrderContextAsync(user, orderCtx, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingReceipt, ct);

        var discountedTotal = Math.Max(0, orderAmount - vr.DiscountAmount);
        await _msg.SendHtmlAsync(user.ChatId,
            $"✅ <b>کد تخفیف اعمال شد!</b>\n" +
            $"🔖 کد: <code>{HtmlSanitizer.Encode(vr.Coupon.Code)}</code>\n" +
            $"💸 تخفیف: <b>{vr.DiscountAmount:F0} تومان</b>\n" +
            $"💰 مبلغ نهایی: <b>{discountedTotal:F0} تومان</b>\n\n" +
            "📸 حالا رسید پرداخت را ارسال کنید:", ct: ct);
        await ShowPaymentInstructionsAsync(user, orderCtx, ct);
    }

    private async Task ShowAffiliateAsync(TelegramUser user, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var planAllowsAffiliate = false;
        if (_tenantContext.IsSet)
        {
            var tenant = await _uow.Tenants.GetByIdAsync(_tenantContext.TenantId);
            planAllowsAffiliate = tenant?.Plan?.AllowsAffiliate ?? false;
        }

        if (!planAllowsAffiliate)
        {
            await _msg.SendHtmlAsync(user.ChatId,
                "❌ این قابلیت در پلن فعلی شما در دسترس نیست.", ct: ct);
            return;
        }

        var result = await _affiliateService.GetOrCreateAffiliateAsync(user.Id);
        if (!result.IsSuccess)
        {
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {result.ErrorMessage}", ct: ct);
            return;
        }

        var affiliate = result.Data!;
        await _audit.LogAsync(user.Id, AuditAction.CreateAffiliate, "Affiliate", affiliate.Id, "Affiliate viewed/created");

        await _msg.SendHtmlAsync(user.ChatId,
            $"🔗 <b>لینک معرفی شما</b>\n\n" +
            $"هر کاربری که با لینک شما ثبت‌نام کند، به کیف پول شما اعتبار هدیه تعلق می‌گیرد.\n\n" +
            $"لینک شما:\n<code>https://t.me/{(await _uow.BotSettings.GetValueAsync("BotUsername") ?? "bot")}?start=ref_{affiliate.ReferralCode}</code>\n\n" +
            $"📊 معرفی‌های موفق: <b>{affiliate.TotalReferrals}</b>\n" +
            $"💰 درآمد کل: <b>{affiliate.TotalEarnings:F0} تومان</b>",
            ct: ct);
    }

    // Shared helper: show payment card and instructions
    private async Task ShowPaymentInstructionsAsync(TelegramUser user, OrderContext ctx, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var cardResult = await _paymentService.GetActivePaymentCardAsync();
        var cardInfo = cardResult.IsSuccess
            ? $"💳 کارت: <code>{cardResult.Data!.CardNumber}</code>\n👤 {HtmlSanitizer.Encode(cardResult.Data.CardHolderName)}\n🏦 {HtmlSanitizer.Encode(cardResult.Data.BankName)}"
            : "💳 برای اطلاعات پرداخت با پشتیبانی تماس بگیرید.";

        var finalAmount = Math.Max(0, ctx.ProductPrice * ctx.Quantity - ctx.DiscountAmount);
        var instruction = await _texts.FormatAsync("PaymentInstructionMessage", lang,
            new() { ["amount"] = $"{finalAmount:F0} تومان" },
            $"💳 مبلغ <b>{finalAmount:F0} تومان</b> را واریز کرده و رسید را ارسال کنید.");

        await _msg.SendHtmlAsync(user.ChatId,
            $"{cardInfo}\n\n{instruction}\n\n📸 حالا رسید را ارسال کنید:",
            await _kb.BuildCancelKeyboardAsync(lang), ct);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task NotifyDbAdminsAsync(string fileId, string caption, InlineKeyboardMarkup kb, CancellationToken ct)
    {
        var admins = await _uow.Users.GetAdminsAsync();
        foreach (var admin in admins.Where(a => a.ChatId > 0))
        {
            try { await _msg.SendPhotoAsync(admin.ChatId, fileId, caption, kb, ct); }
            catch { /* ignore individual failures */ }
        }
    }

    private async Task NotifyOrderUserAsync(int orderId, string messageKey, Dictionary<string, string> vars, CancellationToken ct)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null) return;
        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
        {
            var text = await _texts.FormatAsync(messageKey, orderUser.PreferredLanguage, vars);
            await _msg.SendHtmlAsync(orderUser.ChatId, text, ct: ct);
        }
    }
}
