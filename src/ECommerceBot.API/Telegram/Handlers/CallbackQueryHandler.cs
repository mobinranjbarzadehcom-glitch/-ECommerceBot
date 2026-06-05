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

        // Reject blank or suspiciously long callback data
        if (string.IsNullOrWhiteSpace(data) || data.Length > 64)
        {
            await _msg.AnswerCallbackAsync(callbackQuery.Id, "Invalid action.", ct: ct);
            return;
        }

        if (user.IsBlocked)
        {
            await _msg.AnswerCallbackAsync(callbackQuery.Id, "You are blocked.", ct: ct);
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
                    await _msg.SendHtmlAsync(chatId, "❌ Admin only.", ct: ct);
                    return;
                }
                if (_rateLimit.IsAdminRateLimited(user.TelegramId))
                {
                    await _msg.SendHtmlAsync(chatId, "⚠️ Too many admin actions. Please wait a minute.", ct: ct);
                    return;
                }
                await HandleAdminCallbackAsync(parts, user, chatId, msgId, ct);
                break;
            case "lic":
                if (user.Role != UserRole.Admin)
                {
                    await _msg.SendHtmlAsync(chatId, "❌ Admin only.", ct: ct);
                    return;
                }
                await HandleLicenseCallbackAsync(parts, user, chatId, ct);
                break;
            default:
                _logger.LogWarning("Unknown callback action '{Action}' from {TelegramId}", action, user.TelegramId);
                break;
        }
    }

    // ─── License callback ────────────────────────────────────────────────────────

    private async Task HandleLicenseCallbackAsync(string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        var sub = parts.ElementAtOrDefault(1);
        switch (sub)
        {
            case "refresh":
                var refreshed = await _licenseService.ValidateAsync(ct);
                var statusMsg = $"🔄 <b>بروزرسانی وضعیت لایسنس</b>\n\nوضعیت: <b>{refreshed.Status}</b>\n{HtmlSanitizer.Encode(refreshed.Message)}";
                await _msg.SendHtmlAsync(chatId, statusMsg, ct: ct);
                break;

            case "activate":
                await _conv.SetStateAsync(user, ConversationState.AwaitingLicenseKey, ct);
                await _msg.SendHtmlAsync(chatId,
                    "🔑 <b>فعال‌سازی لایسنس</b>\n\nلطفاً کد لایسنس کامل را وارد کنید:",
                    new ReplyKeyboardMarkup("❌ Cancel") { ResizeKeyboard = true }, ct);
                break;

            case "fingerprint":
                var fp = _fingerprintService.GetFingerprint();
                await _msg.SendHtmlAsync(chatId,
                    $"🖥 <b>اثر انگشت سرور</b>\n\n<code>{fp}</code>\n\nاین مقدار را به فروشنده ارائه دهید تا لایسنس را به این سرور متصل کند.",
                    ct: ct);
                break;

            default:
                await _msg.SendHtmlAsync(chatId, "❌ عملیات ناشناخته.", ct: ct);
                break;
        }
    }

    // ─── User callback flows ─────────────────────────────────────────────────────

    private async Task HandleMenuCallbackAsync(string[] parts, TelegramUser user, long chatId, int msgId, CancellationToken ct)
    {
        var sub = parts.ElementAtOrDefault(1);
        switch (sub)
        {
            case "products":
            case "main":
                var cats = (await _uow.Categories.GetActiveCategoriesAsync()).ToList();
                if (cats.Count == 0)
                {
                    await _msg.SendHtmlAsync(chatId, "😔 No categories available.", ct: ct);
                    return;
                }
                var kb = _kb.BuildCategoriesKeyboard(cats.Select(c => (c.Id, c.Name)));
                await _msg.SendHtmlAsync(chatId, "🛒 <b>Select a category:</b>", kb, ct);
                break;
        }
    }

    private async Task HandleCategoryCallbackAsync(string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(parts.ElementAtOrDefault(1), out var catId)) return;

        var category = await _uow.Categories.GetByIdAsync(catId);
        if (category is null || !category.IsActive)
        {
            await _msg.SendHtmlAsync(chatId, "❌ Category not found or disabled.", ct: ct);
            return;
        }

        var products = (await _uow.Products.GetByCategoryAsync(catId)).ToList();
        if (products.Count == 0)
        {
            await _msg.SendHtmlAsync(chatId, "😔 No products in this category.", _kb.BuildBackButton("menu:products"), ct);
            return;
        }

        var kb = _kb.BuildProductsKeyboard(products.Select(p => (p.Id, p.Name, p.Price)), catId);
        await _msg.SendHtmlAsync(chatId, $"🛒 <b>{category.Name}</b>\n\nSelect a product:", kb, ct);
    }

    private async Task HandleProductCallbackAsync(string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(parts.ElementAtOrDefault(1), out var productId)) return;

        var product = await _uow.Products.GetWithKeysAsync(productId);
        if (product is null || product.Status != ProductStatus.Active)
        {
            await _msg.SendHtmlAsync(chatId, "❌ Product not found or unavailable.", ct: ct);
            return;
        }

        var availableKeys = await _uow.Products.GetAvailableKeyCountAsync(productId);

        var html = $"📦 <b>{product.Name}</b>\n" +
                   $"💰 Price: <b>{product.Price:F2}$</b>\n" +
                   (product.Description is not null ? $"📝 {product.Description}\n" : "") +
                   $"🔑 Available: {availableKeys}";

        if (availableKeys == 0)
        {
            await _msg.SendHtmlAsync(chatId, html + "\n\n❌ <i>Out of stock</i>", _kb.BuildBackButton($"cat:{product.CategoryId}"), ct);
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

        await _msg.SendHtmlAsync(chatId,
            html + "\n\n🎮 <b>Please enter your Player ID / Account details:</b>",
            new ReplyKeyboardMarkup("❌ Cancel") { ResizeKeyboard = true }, ct);
    }

    private async Task HandleOrderCallbackAsync(string[] parts, TelegramUser user, long chatId, int msgId, CancellationToken ct)
    {
        var sub = parts.ElementAtOrDefault(1);

        if (!int.TryParse(parts.ElementAtOrDefault(2), out var orderId)) return;

        if (user.Role != UserRole.Admin)
        {
            await _msg.SendHtmlAsync(chatId, "❌ Admin only.", ct: ct);
            return;
        }

        switch (sub)
        {
            case "approve":
                await HandleAdminApproveAsync(orderId, user, chatId, msgId, ct);
                break;
            case "reject":
                await HandleAdminRejectInitAsync(orderId, user, chatId, ct);
                break;
            case "newreceipt":
                await HandleAdminRequestNewReceiptAsync(orderId, user, chatId, ct);
                break;
            case "refund":
                await HandleAdminRefundAsync(orderId, user, chatId, ct);
                break;
        }
    }

    private async Task HandleAdminApproveAsync(int orderId, TelegramUser admin, long chatId, int msgId, CancellationToken ct)
    {
        var result = await _adminService.ApproveOrderAsync(orderId, admin.Id);
        if (!result.IsSuccess)
        {
            await _msg.SendHtmlAsync(chatId, $"❌ {result.ErrorMessage}", ct: ct);
            return;
        }

        await _msg.EditHtmlAsync(chatId, msgId, $"✅ Order #{orderId} approved.", null, ct);

        // Get order details and notify user
        var order = await _uow.Orders.GetOrderWithItemsAndKeysAsync(orderId);
        if (order is null) return;

        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
        {
            var keysText = string.Join("\n", order.OrderItems
                .SelectMany(oi => oi.ProductKeys)
                .Select(k => $"🔑 <code>{k.KeyValue}</code>"));

            var notification = await _texts.FormatAsync("OrderApprovedMessage",
                new() { ["orderId"] = orderId.ToString(), ["keys"] = keysText },
                $"✅ <b>Order #{orderId} approved!</b>\n\nYour keys:\n{keysText}");

            await _msg.SendHtmlAsync(orderUser.ChatId, notification,
                await _kb.BuildMainMenuAsync(), ct);
        }
    }

    private async Task HandleAdminRejectInitAsync(int orderId, TelegramUser admin, long chatId, CancellationToken ct)
    {
        await _conv.SetAdminContextAsync(admin, new AdminContext { TargetOrderId = orderId }, ct);
        await _conv.SetStateAsync(admin, ConversationState.AwaitingRejectReason, ct);
        await _msg.SendHtmlAsync(chatId,
            $"📝 Enter rejection reason for Order #{orderId}:",
            new ReplyKeyboardMarkup("❌ Cancel") { ResizeKeyboard = true }, ct);
    }

    private async Task HandleAdminRequestNewReceiptAsync(int orderId, TelegramUser admin, long chatId, CancellationToken ct)
    {
        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null) return;

        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
        {
            await _msg.SendHtmlAsync(orderUser.ChatId,
                $"🔄 <b>Admin is requesting a new receipt</b> for Order #{orderId}.\n\nPlease send a new receipt photo.", ct: ct);
        }
        await _msg.SendHtmlAsync(chatId, $"✅ New receipt requested from user for Order #{orderId}.", ct: ct);
    }

    private async Task HandleAdminRefundAsync(int orderId, TelegramUser admin, long chatId, CancellationToken ct)
    {
        var order = await _uow.Orders.GetOrderWithTransactionAsync(orderId);
        if (order is null) { await _msg.SendHtmlAsync(chatId, "❌ Order not found.", ct: ct); return; }

        var result = await _userService.RefundToWalletAsync(order.UserId, order.TotalAmount, orderId, $"Admin refund for order #{orderId}");
        if (!result.IsSuccess) { await _msg.SendHtmlAsync(chatId, $"❌ {result.ErrorMessage}", ct: ct); return; }

        await _audit.LogAsync(admin.Id, AuditAction.RefundOrder, "Order", orderId, $"Amount={order.TotalAmount:F2}");
        await _msg.SendHtmlAsync(chatId, $"✅ Refunded {order.TotalAmount:F2}$ to user for Order #{orderId}.", ct: ct);

        var orderUser = await _uow.Users.GetByIdAsync(order.UserId);
        if (orderUser?.ChatId > 0)
            await _msg.SendHtmlAsync(orderUser.ChatId, $"💸 <b>Refund received!</b> {order.TotalAmount:F2}$ has been added to your wallet.", ct: ct);
    }

    // ─── Admin CMS callbacks ─────────────────────────────────────────────────────

    private async Task HandleAdminCallbackAsync(string[] parts, TelegramUser user, long chatId, int msgId, CancellationToken ct)
    {
        var entity = parts.ElementAtOrDefault(1); // "cat", "prod", "card", "set"
        var idStr = parts.ElementAtOrDefault(2);

        switch (entity)
        {
            case "cat":
                await HandleAdminCatCallbackAsync(idStr, parts, user, chatId, ct);
                break;
            case "prod":
                await HandleAdminProdCallbackAsync(idStr, parts, user, chatId, ct);
                break;
            case "card":
                await HandleAdminCardCallbackAsync(idStr, parts, user, chatId, ct);
                break;
            case "set":
                await HandleAdminSettingCallbackAsync(idStr, user, chatId, ct);
                break;
        }
    }

    private async Task HandleAdminCatCallbackAsync(string? idStr, string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        if (idStr == "add")
        {
            await _conv.SetAdminContextAsync(user, new AdminContext(), ct);
            await _conv.SetStateAsync(user, ConversationState.AwaitingCategoryName, ct);
            await _msg.SendHtmlAsync(chatId, "📝 Enter new category name:", ct: ct);
            return;
        }

        if (!int.TryParse(idStr, out var catId)) return;
        var category = await _uow.Categories.GetByIdAsync(catId);
        if (category is null) { await _msg.SendHtmlAsync(chatId, "❌ Category not found.", ct: ct); return; }

        var action = parts.ElementAtOrDefault(3);
        switch (action)
        {
            case "rename":
                await _conv.SetAdminContextAsync(user, new AdminContext { TargetCategoryId = catId }, ct);
                await _conv.SetStateAsync(user, ConversationState.AwaitingCategoryName, ct);
                await _msg.SendHtmlAsync(chatId, $"📝 Enter new name for <b>{category.Name}</b>:", ct: ct);
                break;
            case "toggle":
                category.IsActive = !category.IsActive;
                _uow.Categories.Update(category);
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id,
                    category.IsActive ? AuditAction.EnableCategory : AuditAction.DisableCategory,
                    "Category", catId);
                await _msg.SendHtmlAsync(chatId, $"✅ Category <b>{HtmlSanitizer.Encode(category.Name)}</b> is now {(category.IsActive ? "enabled" : "disabled")}.", ct: ct);
                break;
            default:
                // Show category details with actions
                var html = $"🗂 <b>{category.Name}</b>\n" +
                           $"Status: {(category.IsActive ? "✅ Active" : "❌ Disabled")}\n" +
                           $"Order: {category.DisplayOrder}";
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("✏️ Rename", $"adm:cat:{catId}:rename") },
                    new[] { InlineKeyboardButton.WithCallbackData(category.IsActive ? "🔴 Disable" : "🟢 Enable", $"adm:cat:{catId}:toggle") },
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back", "adm:cat:list") }
                });
                await _msg.SendHtmlAsync(chatId, html, kb, ct);
                break;
        }
    }

    private async Task HandleAdminProdCallbackAsync(string? idStr, string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        if (idStr == "add")
        {
            await _msg.SendHtmlAsync(chatId, "ℹ️ To add a product, please use the web CMS. Telegram-based product creation coming soon.", ct: ct);
            return;
        }

        if (!int.TryParse(idStr, out var prodId)) return;
        var product = await _uow.Products.GetByIdAsync(prodId);
        if (product is null) { await _msg.SendHtmlAsync(chatId, "❌ Product not found.", ct: ct); return; }

        var action = parts.ElementAtOrDefault(3);
        switch (action)
        {
            case "rename":
                await _conv.SetAdminContextAsync(user, new AdminContext { TargetProductId = prodId }, ct);
                await _conv.SetStateAsync(user, ConversationState.AwaitingProductTitle, ct);
                await _msg.SendHtmlAsync(chatId, $"📝 Enter new title for <b>{product.Name}</b>:", ct: ct);
                break;
            case "price":
                await _conv.SetAdminContextAsync(user, new AdminContext { TargetProductId = prodId }, ct);
                await _conv.SetStateAsync(user, ConversationState.AwaitingProductPrice, ct);
                await _msg.SendHtmlAsync(chatId, $"💰 Enter new price for <b>{product.Name}</b>:", ct: ct);
                break;
            case "toggle":
                product.Status = product.Status == ProductStatus.Active ? ProductStatus.Inactive : ProductStatus.Active;
                _uow.Products.Update(product);
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id,
                    product.Status == ProductStatus.Active ? AuditAction.EnableProduct : AuditAction.DisableProduct,
                    "Product", prodId);
                await _msg.SendHtmlAsync(chatId, $"✅ Product <b>{HtmlSanitizer.Encode(product.Name)}</b> is now {product.Status}.", ct: ct);
                break;
            default:
                var keys = await _uow.Products.GetAvailableKeyCountAsync(prodId);
                var html = $"📦 <b>{product.Name}</b>\n💰 {product.Price:F2}$\n🔑 Keys: {keys}\nStatus: {product.Status}";
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("✏️ Rename", $"adm:prod:{prodId}:rename"), InlineKeyboardButton.WithCallbackData("💰 Price", $"adm:prod:{prodId}:price") },
                    new[] { InlineKeyboardButton.WithCallbackData(product.Status == ProductStatus.Active ? "🔴 Disable" : "🟢 Enable", $"adm:prod:{prodId}:toggle") }
                });
                await _msg.SendHtmlAsync(chatId, html, kb, ct);
                break;
        }
    }

    private async Task HandleAdminCardCallbackAsync(string? idStr, string[] parts, TelegramUser user, long chatId, CancellationToken ct)
    {
        if (idStr == "add")
        {
            await _conv.SetAdminContextAsync(user, new AdminContext(), ct);
            await _conv.SetStateAsync(user, ConversationState.AwaitingCardNumber, ct);
            await _msg.SendHtmlAsync(chatId, "💳 Enter card number:", ct: ct);
            return;
        }

        if (!int.TryParse(idStr, out var cardId)) return;
        var card = await _uow.PaymentCards.GetByIdAsync(cardId);
        if (card is null) { await _msg.SendHtmlAsync(chatId, "❌ Card not found.", ct: ct); return; }

        var action = parts.ElementAtOrDefault(3);
        switch (action)
        {
            case "toggle":
                card.IsActive = !card.IsActive;
                _uow.PaymentCards.Update(card);
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id, card.IsActive ? AuditAction.EditCard : AuditAction.DisableCard, "PaymentCard", cardId);
                await _msg.SendHtmlAsync(chatId, $"✅ Card {HtmlSanitizer.Encode(card.CardNumber)} is now {(card.IsActive ? "active" : "inactive")}.", ct: ct);
                break;
            case "default":
                var allCards = (await _uow.PaymentCards.GetAllAsync()).ToList();
                foreach (var c in allCards) { c.IsDefault = c.Id == cardId; _uow.PaymentCards.Update(c); }
                await _uow.SaveChangesAsync(ct);
                await _audit.LogAsync(user.Id, AuditAction.SetDefaultCard, "PaymentCard", cardId);
                await _msg.SendHtmlAsync(chatId, $"✅ Card {HtmlSanitizer.Encode(card.CardNumber)} set as default.", ct: ct);
                break;
            default:
                var html = $"💳 <b>{card.CardNumber}</b>\n👤 {card.CardHolderName}\n🏦 {card.BankName}\n" +
                           $"Status: {(card.IsActive ? "✅" : "❌")} {(card.IsDefault ? "⭐ Default" : "")}";
                var kb = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData(card.IsActive ? "🔴 Disable" : "🟢 Enable", $"adm:card:{cardId}:toggle"), InlineKeyboardButton.WithCallbackData("⭐ Set Default", $"adm:card:{cardId}:default") }
                });
                await _msg.SendHtmlAsync(chatId, html, kb, ct);
                break;
        }
    }

    private async Task HandleAdminSettingCallbackAsync(string? key, TelegramUser user, long chatId, CancellationToken ct)
    {
        if (key == "list")
        {
            var keys = new[] { "WelcomeMessage", "HelpMessage", "PaymentInstructionMessage", "MainMenu.ProductsButton" };
            var rows = keys.Select(k => new[] { InlineKeyboardButton.WithCallbackData(k, $"adm:set:{k}") }).ToList();
            await _msg.SendHtmlAsync(chatId, "⚙️ Select a setting to edit:", new InlineKeyboardMarkup(rows), ct);
            return;
        }

        if (string.IsNullOrEmpty(key)) return;

        var current = await _texts.GetAsync(key, "—");
        await _conv.SetAdminContextAsync(user, new AdminContext { TargetSettingKey = key }, ct);
        await _conv.SetStateAsync(user, ConversationState.AwaitingSettingValue, ct);
        await _msg.SendHtmlAsync(chatId,
            $"⚙️ <b>Editing:</b> <code>{key}</code>\n\nCurrent value:\n{current}\n\nSend new value:", ct: ct);
    }
}
