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

public class CallbackQueryHandler : ICallbackQueryHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IOrderService _orderService;
    private readonly IAdminService _adminService;
    private readonly IUserService _userService;
    private readonly IPaymentService _paymentService;
    private readonly ITelegramMessageService _msg;
    private readonly IKeyboardBuilder _kb;
    private readonly IBotTextService _texts;
    private readonly IConversationManager _conv;
    private readonly IAuditLogService _audit;
    private readonly IRateLimitService _rateLimit;
    private readonly ILicenseService _licenseService;
    private readonly IServerFingerprintService _fingerprintService;
    private readonly ILogger<CallbackQueryHandler> _logger;

    public CallbackQueryHandler(
        IUnitOfWork uow,
        IOrderService orderService,
        IAdminService adminService,
        IUserService userService,
        IPaymentService paymentService,
        ITelegramMessageService msg,
        IKeyboardBuilder kb,
        IBotTextService texts,
        IConversationManager conv,
        IAuditLogService audit,
        IRateLimitService rateLimit,
        ILicenseService licenseService,
        IServerFingerprintService fingerprintService,
        ILogger<CallbackQueryHandler> logger)
    {
        _uow = uow;
        _orderService = orderService;
        _adminService = adminService;
        _userService = userService;
        _paymentService = paymentService;
        _msg = msg;
        _kb = kb;
        _texts = texts;
        _conv = conv;
        _audit = audit;
        _rateLimit = rateLimit;
        _licenseService = licenseService;
        _fingerprintService = fingerprintService;
        _logger = logger;
    }

    public async Task HandleAsync(CallbackQuery callbackQuery, TelegramUser user, CancellationToken ct = default)
    {
        var data = callbackQuery.Data ?? string.Empty;
        var chatId = callbackQuery.Message?.Chat.Id ?? user.ChatId;
        var msgId = callbackQuery.Message?.MessageId ?? 0;
        var lang = user.PreferredLanguage;

        if (string.IsNullOrWhiteSpace(data) || data.Length > 64)
        {
            await _msg.AnswerCallbackAsync(callbackQuery.Id,
                await _texts.GetAsync("Callback.InvalidAction", lang, "Invalid action."), ct: ct);
            return;
        }

        if (user.IsBlocked)
        {
            await _msg.AnswerCallbackAsync(callbackQuery.Id,
                await _texts.GetAsync("Callback.Blocked", lang, "You are blocked."), ct: ct);
            return;
        }

        await _msg.AnswerCallbackAsync(callbackQuery.Id, ct: ct);

        var parts = data.Split(':');
        var action = parts[0];

        switch (action)
        {
            case "menu":
                await HandleMenuCallbackAsync(parts, user, chatId, msgId, ct);
                break;
            case "cat":
                await HandleCategoryCallbackAsync(parts, user, chatId, ct);
                break;
            case "prod":
                await HandleProductCallbackAsync(parts, user, chatId, ct);
                break;
            case "order":
                await HandleOrderCallbackAsync(parts, user, chatId, msgId, ct);
                break;
            case "adm":
                if (user.Role != UserRole.Admin)
                {
                    _logger.LogWarning("Non-admin {TelegramId} attempted admin callback: {Data}", user.TelegramId, data);
                    await _msg.SendHtmlAsync(chatId,
                        await _texts.GetAsync("Admin.Only", lang, "❌ Admin only."), ct: ct);
                    return;
                }
                if (_rateLimit.IsAdminRateLimited(user.TelegramId))
                {
                    await _msg.SendHtmlAsync(chatId,
                        await _texts.GetAsync("Admin.TooManyActions", lang, "⚠️ Too many admin actions. Please wait a minute."), ct: ct);
                    return;
                }
                await HandleAdminCallbackAsync(parts, user, chatId, msgId, ct);
                break;
            case "lic":
                if (user.Role != UserRole.Admin)
                {
                    await _msg.SendHtmlAsync(chatId,
                        await _texts.GetAsync("Admin.Only", lang, "❌ Admin only."), ct: ct);
                    return;
                }
                await HandleLicenseCallbackAsync(parts, user, chatId, ct);
                break;
            default:
                _logger.LogWarning("Unknown callback action '{Action}' from {TelegramId}", action, user.TelegramId);
                break;
        }
    }

    // ─── License callbacks ───────────────────────────────────────────────────

    private async Task HandleLicenseCallbackAsync(string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var sub = parts.ElementAtOrDefault(1);
        switch (sub)
        {
            case "refresh":
                var refreshed = await _licenseService.ValidateAsync(ct);
                var statusMsg = await _texts.FormatAsync("License.RefreshStatus", lang,
                    new() { ["status"] = refreshed.Status.ToString(), ["message"] = HtmlSanitizer.Encode(refreshed.Message) },
                    $"🔄 <b>License Status Updated</b>\n\nStatus: <b>{refreshed.Status}</b>\n{HtmlSanitizer.Encode(refreshed.Message)}");
                await _msg.SendHtmlAsync(chatId, statusMsg, ct: ct);
                break;

            case "activate":
                await _conv.SetStateAsync(user, ConversationState.AwaitingLicenseKey, ct);
                await _msg.SendHtmlAsync(chatId,
                    await _texts.GetAsync("License.ActivatePrompt", lang,
                        "🔑 <b>License Activation</b>\n\nPlease enter the full license key:"),
                    await _kb.BuildCancelKeyboardAsync(lang), ct);
                break;

            case "fingerprint":
                var fp = _fingerprintService.GetFingerprint();
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("License.FingerprintTitle", lang,
                        new() { ["fingerprint"] = fp },
                        $"🖥 <b>Server Fingerprint</b>\n\n<code>{fp}</code>\n\nProvide this to your vendor to bind the license to this server."),
                    ct: ct);
                break;

            default:
                await _msg.SendHtmlAsync(chatId,
                    await _texts.GetAsync("License.UnknownAction", lang, "❌ Unknown action."), ct: ct);
                break;
        }
    }

    // ─── User callback flows ─────────────────────────────────────────────────

    private async Task HandleMenuCallbackAsync(string[] parts, TelegramUser user, long chatId, int msgId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var sub = parts.ElementAtOrDefault(1);
        switch (sub)
        {
            case "products":
            case "main":
                var cats = (await _uow.Categories.GetActiveCategoriesAsync()).ToList();
                if (cats.Count == 0)
                {
                    await _msg.SendHtmlAsync(chatId,
                        await _texts.GetAsync("Products.NoCategoriesAvailable", lang, "😔 No categories available."), ct: ct);
                    return;
                }
                var kb = _kb.BuildCategoriesKeyboard(cats.Select(c => (c.Id, c.Name)));
                await _msg.SendHtmlAsync(chatId,
                    await _texts.GetAsync("Products.SelectCategory", lang, "🛒 <b>Select a category:</b>"), kb, ct);
                break;
        }
    }

    private async Task HandleCategoryCallbackAsync(string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        if (!int.TryParse(parts.ElementAtOrDefault(1), out var catId)) return;

        var category = await _uow.Categories.GetByIdAsync(catId);
        if (category is null || !category.IsActive)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Products.CategoryNotFound", lang, "❌ Category not found or disabled."), ct: ct);
            return;
        }

        var products = (await _uow.Products.GetByCategoryAsync(catId)).ToList();
        if (products.Count == 0)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Products.NoneInCategory", lang, "😔 No products in this category."),
                await _kb.BuildBackButtonAsync("menu:products", lang), ct);
            return;
        }

        var kb = _kb.BuildProductsKeyboard(products.Select(p => (p.Id, p.Name, p.Price)), catId);
        await _msg.SendHtmlAsync(chatId,
            await _texts.FormatAsync("Products.SelectProduct", lang,
                new() { ["name"] = category.Name },
                $"🛒 <b>{category.Name}</b>\n\nSelect a product:"),
            kb, ct);
    }

    private async Task HandleProductCallbackAsync(string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        if (!int.TryParse(parts.ElementAtOrDefault(1), out var productId)) return;

        var product = await _uow.Products.GetWithKeysAsync(productId);
        if (product is null || product.Status != ProductStatus.Active)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Products.NotFound", lang, "❌ Product not found or unavailable."), ct: ct);
            return;
        }

        var availableKeys = await _uow.Products.GetAvailableKeyCountAsync(productId);

        var html = $"📦 <b>{product.Name}</b>\n" +
                   $"💰 Price: <b>{product.Price:F2}$</b>\n" +
                   (product.Description is not null ? $"📝 {product.Description}\n" : "") +
                   $"🔑 Available: {availableKeys}";

        if (availableKeys == 0)
        {
            var outOfStock = await _texts.GetAsync("Products.OutOfStock", lang, "❌ <i>Out of stock</i>");
            await _msg.SendHtmlAsync(chatId,
                html + $"\n\n{outOfStock}",
                await _kb.BuildBackButtonAsync($"cat:{product.CategoryId}", lang), ct);
            return;
        }

        var ctx = new OrderContext
        {
            ProductId = product.Id,
            ProductName = product.Name,
            ProductPrice = product.Price,
            Quantity = 1,
            CategoryId = product.CategoryId
        };
        await _conv.SetOrderContextAsync(user, ctx, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingPlayerId, ct);

        var enterPlayerId = await _texts.GetAsync("Products.EnterPlayerId", lang,
            "🎮 <b>Please enter your Player ID / Account details:</b>");
        await _msg.SendHtmlAsync(chatId,
            html + $"\n\n{enterPlayerId}",
            await _kb.BuildCancelKeyboardAsync(lang), ct);
    }

    private async Task HandleOrderCallbackAsync(string[] parts, TelegramUser user, long chatId, int msgId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;
        var sub = parts.ElementAtOrDefault(1);

        if (!int.TryParse(parts.ElementAtOrDefault(2), out var orderId)) return;

        if (user.Role != UserRole.Admin)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.Only", lang, "❌ Admin only."), ct: ct);
            return;
        }

        switch (sub)
        {
            case "approve":   await HandleAdminApproveAsync(orderId, user, chatId, msgId, ct); break;
            case "reject":    await HandleAdminRejectInitAsync(orderId, user, chatId, ct); break;
            case "newreceipt": await HandleAdminRequestNewReceiptAsync(orderId, user, chatId, ct); break;
            case "refund":    await HandleAdminRefundAsync(orderId, user, chatId, ct); break;
        }
    }

    private async Task HandleAdminApproveAsync(int orderId, TelegramUser admin, long chatId, int msgId, CancellationToken ct)
    {
        var lang = admin.PreferredLanguage;
        var result = await _adminService.ApproveOrderAsync(orderId, admin.Id);
        if (!result.IsSuccess)
        {
            await _msg.SendHtmlAsync(chatId, $"❌ {result.ErrorMessage}", ct: ct);
            return;
        }

        var approvedMsg = await _texts.FormatAsync("Admin.OrderApproved", lang,
            new() { ["orderId"] = orderId.ToString() },
            $"✅ Order #{orderId} approved.");
        await _msg.EditHtmlAsync(chatId, msgId, approvedMsg, null, ct);

        var order = await _uow.Orders.GetOrderWithItemsAndKeysAsync(orderId);
        if (order is null) return;

        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
        {
            var keysText = string.Join("\n", order.OrderItems
                .SelectMany(oi => oi.ProductKeys)
                .Select(k => $"🔑 <code>{k.KeyValue}</code>"));

            var notification = await _texts.FormatAsync("OrderApprovedMessage", orderUser.PreferredLanguage,
                new() { ["orderId"] = orderId.ToString(), ["keys"] = keysText },
                $"✅ <b>Order #{orderId} approved!</b>\n\nYour keys:\n{keysText}");

            await _msg.SendHtmlAsync(orderUser.ChatId, notification,
                await _kb.BuildMainMenuAsync(orderUser.PreferredLanguage), ct);
        }
    }

    private async Task HandleAdminRejectInitAsync(int orderId, TelegramUser admin, long chatId, CancellationToken ct)
    {
        var lang = admin.PreferredLanguage;
        await _conv.SetAdminContextAsync(admin, new AdminContext { TargetOrderId = orderId }, ct);
        await _conv.SetStateAsync(admin, ConversationState.AwaitingRejectReason, ct);
        await _msg.SendHtmlAsync(chatId,
            await _texts.FormatAsync("Admin.RejectPrompt", lang,
                new() { ["orderId"] = orderId.ToString() },
                $"📝 Enter rejection reason for Order #{orderId}:"),
            await _kb.BuildCancelKeyboardAsync(lang), ct);
    }

    private async Task HandleAdminRequestNewReceiptAsync(int orderId, TelegramUser admin, long chatId, CancellationToken ct)
    {
        var lang = admin.PreferredLanguage;
        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null) return;

        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
        {
            var userMsg = await _texts.FormatAsync("Admin.UserNewReceiptRequest", orderUser.PreferredLanguage,
                new() { ["orderId"] = orderId.ToString() },
                $"🔄 <b>Admin is requesting a new receipt</b> for Order #{orderId}.\n\nPlease send a new receipt photo.");
            await _msg.SendHtmlAsync(orderUser.ChatId, userMsg, ct: ct);
        }

        var adminMsg = await _texts.FormatAsync("Admin.NewReceiptRequested", lang,
            new() { ["orderId"] = orderId.ToString() },
            $"✅ New receipt requested from user for Order #{orderId}.");
        await _msg.SendHtmlAsync(chatId, adminMsg, ct: ct);
    }

    private async Task HandleAdminRefundAsync(int orderId, TelegramUser admin, long chatId, CancellationToken ct)
    {
        var lang = admin.PreferredLanguage;
        var order = await _uow.Orders.GetOrderWithTransactionAsync(orderId);
        if (order is null)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.OrderNotFound", lang, "❌ Order not found."), ct: ct);
            return;
        }

        var result = await _userService.RefundToWalletAsync(order.UserId, order.TotalAmount, orderId, $"Admin refund for order #{orderId}");
        if (!result.IsSuccess)
        {
            await _msg.SendHtmlAsync(chatId, $"❌ {result.ErrorMessage}", ct: ct);
            return;
        }

        await _audit.LogAsync(admin.Id, AuditAction.RefundOrder, "Order", orderId, $"Amount={order.TotalAmount:F2}");

        var adminConfirm = await _texts.FormatAsync("Admin.RefundSuccess", lang,
            new() { ["amount"] = order.TotalAmount.ToString("F2"), ["orderId"] = orderId.ToString() },
            $"✅ Refunded {order.TotalAmount:F2}$ to user for Order #{orderId}.");
        await _msg.SendHtmlAsync(chatId, adminConfirm, ct: ct);

        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
        {
            var userMsg = await _texts.FormatAsync("User.RefundReceived", orderUser.PreferredLanguage,
                new() { ["amount"] = order.TotalAmount.ToString("F2") },
                $"💸 <b>Refund received!</b> {order.TotalAmount:F2}$ has been added to your wallet.");
            await _msg.SendHtmlAsync(orderUser.ChatId, userMsg, ct: ct);
        }
    }

    // ─── Admin CMS callbacks ─────────────────────────────────────────────────

    private async Task HandleAdminCallbackAsync(string[] parts, TelegramUser user, long chatId, int msgId, CancellationToken ct)
    {
        var entity = parts.ElementAtOrDefault(1);
        var idStr  = parts.ElementAtOrDefault(2);

        switch (entity)
        {
            case "cat":  await HandleAdminCatCallbackAsync(idStr, parts, user, chatId, ct); break;
            case "prod": await HandleAdminProdCallbackAsync(idStr, parts, user, chatId, ct); break;
            case "card": await HandleAdminCardCallbackAsync(idStr, parts, user, chatId, ct); break;
            case "set":  await HandleAdminSettingCallbackAsync(idStr, user, chatId, ct); break;
        }
    }

    private async Task HandleAdminCatCallbackAsync(string? idStr, string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;

        if (idStr == "add")
        {
            await _conv.SetAdminContextAsync(user, new AdminContext(), ct);
            await _conv.SetStateAsync(user, ConversationState.AwaitingCategoryName, ct);
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.CatEnterName", lang, "📝 Enter new category name:"), ct: ct);
            return;
        }

        if (!int.TryParse(idStr, out var catId)) return;
        var category = await _uow.Categories.GetByIdAsync(catId);
        if (category is null)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.CatNotFound", lang, "❌ Category not found."), ct: ct);
            return;
        }

        var action = parts.ElementAtOrDefault(3);
        switch (action)
        {
            case "rename":
                await _conv.SetAdminContextAsync(user, new AdminContext { TargetCategoryId = catId }, ct);
                await _conv.SetStateAsync(user, ConversationState.AwaitingCategoryName, ct);
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.CatEnterNewName", lang,
                        new() { ["name"] = category.Name },
                        $"📝 Enter new name for <b>{category.Name}</b>:"), ct: ct);
                break;

            case "toggle":
                category.IsActive = !category.IsActive;
                _uow.Categories.Update(category);
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id,
                    category.IsActive ? AuditAction.EnableCategory : AuditAction.DisableCategory,
                    "Category", catId);
                var statusWord = category.IsActive
                    ? await _texts.GetAsync("Admin.CatEnabled",  lang, "enabled")
                    : await _texts.GetAsync("Admin.CatDisabled", lang, "disabled");
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.CatToggled", lang,
                        new() { ["name"] = HtmlSanitizer.Encode(category.Name), ["status"] = statusWord },
                        $"✅ Category <b>{HtmlSanitizer.Encode(category.Name)}</b> is now {statusWord}."), ct: ct);
                break;

            default:
                var renameBtn  = await _texts.GetAsync("Admin.CatRenameButton",  lang, "✏️ Rename");
                var toggleBtn  = category.IsActive
                    ? await _texts.GetAsync("Admin.CatDisableButton", lang, "🔴 Disable")
                    : await _texts.GetAsync("Admin.CatEnableButton",  lang, "🟢 Enable");
                var backBtn    = await _texts.GetAsync("Buttons.BackButton", lang, "⬅️ Back");
                var html = $"🗂 <b>{category.Name}</b>\n" +
                           $"Status: {(category.IsActive ? "✅ Active" : "❌ Disabled")}\n" +
                           $"Order: {category.DisplayOrder}";
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData(renameBtn, $"adm:cat:{catId}:rename") },
                    new[] { InlineKeyboardButton.WithCallbackData(toggleBtn, $"adm:cat:{catId}:toggle") },
                    new[] { InlineKeyboardButton.WithCallbackData(backBtn,   "adm:cat:list") }
                });
                await _msg.SendHtmlAsync(chatId, html, kb, ct);
                break;
        }
    }

    private async Task HandleAdminProdCallbackAsync(string? idStr, string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;

        if (idStr == "add")
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.ProdAddInfo", lang,
                    "ℹ️ To add a product, please use the web CMS. Telegram-based product creation coming soon."), ct: ct);
            return;
        }

        if (!int.TryParse(idStr, out var prodId)) return;
        var product = await _uow.Products.GetByIdAsync(prodId);
        if (product is null)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.ProdNotFound", lang, "❌ Product not found."), ct: ct);
            return;
        }

        var action = parts.ElementAtOrDefault(3);
        switch (action)
        {
            case "rename":
                await _conv.SetAdminContextAsync(user, new AdminContext { TargetProductId = prodId }, ct);
                await _conv.SetStateAsync(user, ConversationState.AwaitingProductTitle, ct);
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.ProdEnterTitle", lang,
                        new() { ["name"] = product.Name },
                        $"📝 Enter new title for <b>{product.Name}</b>:"), ct: ct);
                break;

            case "price":
                await _conv.SetAdminContextAsync(user, new AdminContext { TargetProductId = prodId }, ct);
                await _conv.SetStateAsync(user, ConversationState.AwaitingProductPrice, ct);
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.ProdEnterPrice", lang,
                        new() { ["name"] = product.Name },
                        $"💰 Enter new price for <b>{product.Name}</b>:"), ct: ct);
                break;

            case "toggle":
                product.Status = product.Status == ProductStatus.Active ? ProductStatus.Inactive : ProductStatus.Active;
                _uow.Products.Update(product);
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id,
                    product.Status == ProductStatus.Active ? AuditAction.EnableProduct : AuditAction.DisableProduct,
                    "Product", prodId);
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.ProdToggled", lang,
                        new() { ["name"] = HtmlSanitizer.Encode(product.Name), ["status"] = product.Status.ToString() },
                        $"✅ Product <b>{HtmlSanitizer.Encode(product.Name)}</b> is now {product.Status}."), ct: ct);
                break;

            default:
                var keys = await _uow.Products.GetAvailableKeyCountAsync(prodId);
                var html = $"📦 <b>{product.Name}</b>\n💰 {product.Price:F2}$\n🔑 Keys: {keys}\nStatus: {product.Status}";
                var renameBtn  = await _texts.GetAsync("Admin.ProdRenameButton",  lang, "✏️ Rename");
                var priceBtn   = await _texts.GetAsync("Admin.ProdPriceButton",   lang, "💰 Price");
                var toggleBtn  = product.Status == ProductStatus.Active
                    ? await _texts.GetAsync("Admin.ProdDisableButton", lang, "🔴 Disable")
                    : await _texts.GetAsync("Admin.ProdEnableButton",  lang, "🟢 Enable");
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData(renameBtn, $"adm:prod:{prodId}:rename"), InlineKeyboardButton.WithCallbackData(priceBtn, $"adm:prod:{prodId}:price") },
                    new[] { InlineKeyboardButton.WithCallbackData(toggleBtn, $"adm:prod:{prodId}:toggle") }
                });
                await _msg.SendHtmlAsync(chatId, html, kb, ct);
                break;
        }
    }

    private async Task HandleAdminCardCallbackAsync(string? idStr, string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;

        if (idStr == "add")
        {
            await _conv.SetAdminContextAsync(user, new AdminContext(), ct);
            await _conv.SetStateAsync(user, ConversationState.AwaitingCardNumber, ct);
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.CardEnterNumber", lang, "💳 Enter card number:"), ct: ct);
            return;
        }

        if (!int.TryParse(idStr, out var cardId)) return;
        var card = await _uow.PaymentCards.GetByIdAsync(cardId);
        if (card is null)
        {
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.CardNotFound", lang, "❌ Card not found."), ct: ct);
            return;
        }

        var action = parts.ElementAtOrDefault(3);
        switch (action)
        {
            case "toggle":
                card.IsActive = !card.IsActive;
                _uow.PaymentCards.Update(card);
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id, card.IsActive ? AuditAction.EditCard : AuditAction.DisableCard, "PaymentCard", cardId);
                var cardStatus = card.IsActive
                    ? await _texts.GetAsync("Admin.CardActive",   lang, "active")
                    : await _texts.GetAsync("Admin.CardInactive", lang, "inactive");
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.CardToggled", lang,
                        new() { ["number"] = HtmlSanitizer.Encode(card.CardNumber), ["status"] = cardStatus },
                        $"✅ Card {HtmlSanitizer.Encode(card.CardNumber)} is now {cardStatus}."), ct: ct);
                break;

            case "default":
                var allCards = (await _uow.PaymentCards.GetAllAsync()).ToList();
                foreach (var c in allCards) { c.IsDefault = c.Id == cardId; _uow.PaymentCards.Update(c); }
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id, AuditAction.SetDefaultCard, "PaymentCard", cardId);
                await _msg.SendHtmlAsync(chatId,
                    await _texts.FormatAsync("Admin.CardSetDefault", lang,
                        new() { ["number"] = HtmlSanitizer.Encode(card.CardNumber) },
                        $"✅ Card {HtmlSanitizer.Encode(card.CardNumber)} set as default."), ct: ct);
                break;

            default:
                var html = $"💳 <b>{card.CardNumber}</b>\n👤 {card.CardHolderName}\n🏦 {card.BankName}\n" +
                           $"Status: {(card.IsActive ? "✅" : "❌")} {(card.IsDefault ? "⭐ Default" : "")}";
                var disableBtn    = card.IsActive
                    ? await _texts.GetAsync("Admin.CardDisableButton",    lang, "🔴 Disable")
                    : await _texts.GetAsync("Admin.CardEnableButton",     lang, "🟢 Enable");
                var setDefaultBtn = await _texts.GetAsync("Admin.CardSetDefaultButton", lang, "⭐ Set Default");
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData(disableBtn, $"adm:card:{cardId}:toggle"), InlineKeyboardButton.WithCallbackData(setDefaultBtn, $"adm:card:{cardId}:default") }
                });
                await _msg.SendHtmlAsync(chatId, html, kb, ct);
                break;
        }
    }

    private async Task HandleAdminSettingCallbackAsync(string? key, TelegramUser user, long chatId, CancellationToken ct)
    {
        var lang = user.PreferredLanguage;

        if (key == "list")
        {
            var keys = new[] { "WelcomeMessage", "HelpMessage", "PaymentInstructionMessage", "MainMenu.ProductsButton",
                               "MainMenu.WalletButton", "MainMenu.OrdersButton", "MainMenu.SupportButton", "MainMenu.HelpButton",
                               "Buttons.CancelButton", "Buttons.BackButton" };
            var rows = keys.Select(k => new[] { InlineKeyboardButton.WithCallbackData(k, $"adm:set:{k}") }).ToList();
            await _msg.SendHtmlAsync(chatId,
                await _texts.GetAsync("Admin.SettingSelectPrompt", lang, "⚙️ Select a setting to edit:"),
                new InlineKeyboardMarkup(rows), ct);
            return;
        }

        if (string.IsNullOrEmpty(key)) return;

        var current = await _texts.GetAsync(key, lang, "—");
        await _conv.SetAdminContextAsync(user, new AdminContext { TargetSettingKey = key }, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingSettingValue, ct);
        await _msg.SendHtmlAsync(chatId,
            await _texts.FormatAsync("Admin.SettingEditPrompt", lang,
                new() { ["key"] = key, ["current"] = current },
                $"⚙️ <b>Editing:</b> <code>{key}</code>\n\nCurrent value:\n{current}\n\nSend new value:"), ct: ct);
    }
}
