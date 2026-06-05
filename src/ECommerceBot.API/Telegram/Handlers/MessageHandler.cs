using ECommerceBot.API.DTOs.Order;
using ECommerceBot.API.DTOs.Ticket;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Audit;
using ECommerceBot.API.Infrastructure.Licensing;
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

        if (user is null)
        {
            await _userService.GetOrCreateUserAsync(from.Id, from.FirstName, from.LastName, from.Username);
            user = await _uow.Users.GetByTelegramIdAsync(from.Id);
            if (user is null) return;
            _logger.LogInformation("New user registered: {TelegramId} @{Username}", from.Id, from.Username ?? "—");
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
        await _conv.SetStateAsync(user, ConversationState.AwaitingReceipt, ct);

        var cardResult = await _paymentService.GetActivePaymentCardAsync();
        var cardInfo = cardResult.IsSuccess
            ? $"💳 Card: <code>{cardResult.Data!.CardNumber}</code>\n👤 {cardResult.Data.CardHolderName}\n🏦 {cardResult.Data.BankName}"
            : "💳 Contact support for payment details.";

        var instruction = await _texts.FormatAsync("PaymentInstructionMessage", lang,
            new() { ["amount"] = $"{ctx.ProductPrice * ctx.Quantity:F2}$" },
            $"💳 Transfer <b>{ctx.ProductPrice * ctx.Quantity:F2}$</b> and send the receipt.");

        await _msg.SendHtmlAsync(user.ChatId,
            $"✅ Player ID saved: <code>{HtmlSanitizer.Encode(playerId)}</code>\n\n{cardInfo}\n\n{instruction}\n\n📸 Now send the receipt photo:",
            await _kb.BuildCancelKeyboardAsync(lang), ct);
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
            AccountDetails = ctx.PlayerId
        };

        var result = await _orderService.CreateOrderAsync(user.Id, request);
        if (!result.IsSuccess)
        {
            await _msg.SendHtmlAsync(user.ChatId, $"❌ {result.ErrorMessage}", ct: ct);
            return;
        }

        await _conv.ClearStateAsync(user, ct);
        var order = result.Data!;

        var pendingMsg = await _texts.FormatAsync("OrderPendingMessage", lang,
            new() { ["orderId"] = order.Id.ToString() },
            $"⏳ <b>Order #{order.Id} submitted!</b> We'll review it shortly.");
        await _msg.SendHtmlAsync(user.ChatId, pendingMsg, await _kb.BuildMainMenuAsync(lang), ct);

        _logger.LogInformation("Order #{OrderId} created by user {TelegramId}", order.Id, user.TelegramId);

        var adminCaption = $"🆕 <b>New Order #{order.Id}</b>\n" +
                           $"👤 User: {HtmlSanitizer.Encode(user.FirstName)} (@{HtmlSanitizer.Encode(user.Username ?? "—")})\n" +
                           $"📦 Product: {HtmlSanitizer.Encode(ctx.ProductName)}\n" +
                           $"💰 Amount: {order.TotalAmount:F2}$\n" +
                           $"🎮 Account: <code>{HtmlSanitizer.Encode(ctx.PlayerId ?? "—")}</code>\n" +
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

        var pendingOrders = await _texts.GetAsync("AdminMenu.OrdersButton",     lang, "📋 Pending Orders");
        var users         = await _texts.GetAsync("AdminMenu.UsersButton",      lang, "👥 Users");
        var products      = await _texts.GetAsync("AdminMenu.ProductsButton",   lang, "📦 Products");
        var categories    = await _texts.GetAsync("AdminMenu.CategoriesButton", lang, "🗂 Categories");
        var cards         = await _texts.GetAsync("AdminMenu.CardsButton",      lang, "💳 Cards");
        var settings      = await _texts.GetAsync("AdminMenu.SettingsButton",   lang, "⚙️ Settings");
        var stats         = await _texts.GetAsync("AdminMenu.StatisticsButton", lang, "📊 Statistics");
        var license       = await _texts.GetAsync("AdminMenu.LicenseButton",    lang, "🔐 License Status");

        if (text == pendingOrders) { await ShowAdminPendingOrdersAsync(user, ct); return; }
        if (text == users)         { await ShowAdminUsersAsync(user, ct); return; }
        if (text == products)      { await ShowAdminProductsAsync(user, ct); return; }
        if (text == categories)    { await ShowAdminCategoriesAsync(user, ct); return; }
        if (text == cards)         { await ShowAdminCardsAsync(user, ct); return; }
        if (text == settings)      { await ShowAdminSettingsAsync(user, ct); return; }
        if (text == stats)         { await ShowAdminStatsAsync(user, ct); return; }
        if (text == license)       { await ShowAdminLicenseStatusAsync(user, ct); return; }

        await _msg.SendHtmlAsync(user.ChatId,
            await _texts.GetAsync("Admin.UseMenu", lang, "Please use the menu."),
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
        var lang = user.PreferredLanguage;
        var keys = new[]
        {
            "WelcomeMessage", "HelpMessage", "PaymentInstructionMessage",
            "MainMenu.ProductsButton", "MainMenu.WalletButton", "MainMenu.OrdersButton",
            "MainMenu.SupportButton", "MainMenu.HelpButton",
            "AdminMenu.OrdersButton", "AdminActions.ApproveButton", "AdminActions.RejectButton",
            "Buttons.CancelButton", "Buttons.BackButton", "Buttons.ConfirmButton",
            "Errors.Blocked", "Errors.RateLimited", "Errors.PlayerIdEmpty",
            "Products.NoCategoriesAvailable", "Products.SelectCategory",
            "Wallet.Title", "Orders.Empty", "Ticket.CreatedSuccess"
        };

        var html = await _texts.GetAsync("Admin.SettingsTitle", lang, "⚙️ <b>Bot Settings:</b>");
        var rows = keys.Select(k =>
            new[] { InlineKeyboardButton.WithCallbackData(k, $"adm:set:{k}") }
        ).ToList();
        await _msg.SendHtmlAsync(user.ChatId, html, new InlineKeyboardMarkup(rows), ct);
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
                await _texts.GetAsync("Admin.Errors.TitleEmpty", lang, "❌ Title cannot be empty."), ct: ct);
            return;
        }

        var ctx = await _conv.GetAdminContextAsync(user);
        await _conv.ClearStateAsync(user, ct);

        if (ctx?.TargetProductId.HasValue == true)
        {
            var prod = await _uow.Products.GetByIdAsync(ctx.TargetProductId.Value);
            if (prod != null) { prod.Name = title; _uow.Products.Update(prod); await _uow.SaveChangesAsync(ct); }
            await _audit.LogAsync(user.Id, AuditAction.EditProductTitle, "Product", ctx.TargetProductId, $"Title: {title}");
            await _msg.SendHtmlAsync(user.ChatId,
                await _texts.FormatAsync("Admin.ProductRenamed", lang,
                    new() { ["title"] = HtmlSanitizer.Encode(title) },
                    $"✅ Product renamed to <b>{HtmlSanitizer.Encode(title)}</b>."),
                await _kb.BuildAdminMenuAsync(lang), ct);
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
