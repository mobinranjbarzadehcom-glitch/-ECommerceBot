using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Security;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.Telegram.Options;
using ECommerceBot.API.Telegram.States;
using ECommerceBot.API.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ECommerceBot.API.Telegram.Handlers;

public class SuperAdminHandler : ISuperAdminHandler
{
    private readonly IUnitOfWork _uow;
    private readonly ITelegramBotClient _bot;
    private readonly IAesEncryptionService _aes;
    private readonly IMemoryCache _cache;
    private readonly TelegramOptions _opts;
    private readonly IRenewalService _renewalService;
    private readonly IBackupManagementService _backupService;
    private readonly IBotHealthService _botHealth;
    private readonly ILogger<SuperAdminHandler> _logger;

    // ── Menu labels ───────────────────────────────────────────────────────────
    private const string BtnDashboard   = "🌐 داشبورد کل";
    private const string BtnTenants     = "👥 مشتریان";
    private const string BtnAddTenant   = "➕ افزودن مشتری";
    private const string BtnPlans       = "📋 پلن‌ها";
    private const string BtnImpersonate = "🔑 ورود به پنل مشتری";
    private const string BtnHealth      = "📡 سلامت سیستم";
    private const string BtnBackup      = "💾 پشتیبان گیری";
    private const string BtnRenewals    = "🔄 درخواست‌های تمدید";
    private const string BtnCancel      = "❌ لغو";
    private const string BtnSkip        = "⬅️ رد شدن";

    public SuperAdminHandler(
        IUnitOfWork uow,
        ITelegramBotClient bot,
        IAesEncryptionService aes,
        IMemoryCache cache,
        IOptions<TelegramOptions> opts,
        IRenewalService renewalService,
        IBackupManagementService backupService,
        IBotHealthService botHealth,
        ILogger<SuperAdminHandler> logger)
    {
        _uow = uow;
        _bot = bot;
        _aes = aes;
        _cache = cache;
        _opts = opts.Value;
        _renewalService = renewalService;
        _backupService = backupService;
        _botHealth = botHealth;
        _logger = logger;
    }

    // ── Entry points ──────────────────────────────────────────────────────────

    public async Task HandleMessageAsync(Message message, CancellationToken ct = default)
    {
        var chatId     = message.Chat.Id;
        var telegramId = message.From?.Id ?? 0;
        var text       = message.Text?.Trim() ?? string.Empty;

        if (text.StartsWith("/start") || text == "/sastart")
        {
            ClearConversation(telegramId);
            await ShowMainMenuAsync(chatId, ct);
            return;
        }

        if (text == BtnCancel)
        {
            ClearConversation(telegramId);
            await ShowMainMenuAsync(chatId, ct);
            return;
        }

        if (text == BtnSkip)
        {
            var conv = GetConversation(telegramId);
            if (conv.State == SuperAdminState.AwaitingCustomerPhone)
            {
                conv.PendingCustomerPhone = null;
                conv.State = SuperAdminState.AwaitingCustomerUsername;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    "👤 یوزرنیم تلگرام ادمین فروشگاه را وارد کنید:\n<i>(بدون @ — یا رد شوید)</i>",
                    BuildSkipCancelKeyboard(), ct);
                return;
            }
            if (conv.State == SuperAdminState.AwaitingCustomerUsername)
            {
                conv.PendingCustomerUsername = null;
                conv.State = SuperAdminState.AwaitingBotToken;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    "🤖 توکن بات تلگرام را وارد کنید:\n<i>(فرمت: 123456789:AAFxxxxxx)</i>",
                    BuildCancelKeyboard(), ct);
                return;
            }
        }

        var convState = GetConversation(telegramId);
        if (convState.State != SuperAdminState.None)
        {
            await HandleWizardStepAsync(chatId, telegramId, text, message, convState, ct);
            return;
        }

        switch (text)
        {
            case BtnDashboard:   await ShowDashboardAsync(chatId, ct); break;
            case BtnTenants:     await ShowTenantsAsync(chatId, 1, ct); break;
            case BtnAddTenant:   await StartAddTenantWizardAsync(chatId, telegramId, ct); break;
            case BtnPlans:       await ShowPlansAsync(chatId, ct); break;
            case BtnImpersonate: await StartImpersonateAsync(chatId, telegramId, ct); break;
            case BtnHealth:      await ShowHealthAsync(chatId, ct); break;
            case BtnBackup:      await ShowBackupMenuAsync(chatId, ct); break;
            case BtnRenewals:    await ShowPendingRenewalsAsync(chatId, ct); break;
            default:
                await SendAsync(chatId, "❓ دستور نامعتبر. از منو استفاده کنید.", BuildMainMenuKeyboard(), ct);
                break;
        }
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken ct = default)
    {
        var chatId     = callbackQuery.Message?.Chat.Id ?? 0;
        var telegramId = callbackQuery.From.Id;
        var data       = callbackQuery.Data ?? string.Empty;

        await AnswerCallbackAsync(callbackQuery.Id, ct);

        var parts = data.Split(':');
        if (parts[0] != "sa") return;

        switch (parts.ElementAtOrDefault(1))
        {
            case "dashboard":
                await ShowDashboardAsync(chatId, ct);
                break;

            case "tenants" when parts.ElementAtOrDefault(2) == "page":
                if (int.TryParse(parts.ElementAtOrDefault(3), out var page))
                    await ShowTenantsAsync(chatId, page, ct);
                break;

            case "tenant":
                await HandleTenantCallbackAsync(parts, chatId, telegramId, ct);
                break;

            case "plan":
                if (int.TryParse(parts.ElementAtOrDefault(2), out var planId))
                    await ShowPlanDetailAsync(chatId, planId, ct);
                break;

            case "setplan":
                await HandleSetPlanCallbackAsync(parts, chatId, ct);
                break;

            case "wizard":
                await HandleWizardCallbackAsync(parts, chatId, telegramId, ct);
                break;

            case "notes":
                await HandleNotesCallbackAsync(parts, chatId, telegramId, ct);
                break;

            case "health":
                await HandleHealthCallbackAsync(parts, chatId, ct);
                break;

            case "backup":
                await HandleBackupCallbackAsync(parts, chatId, callbackQuery.Message?.MessageId ?? 0, ct);
                break;

            case "renewal":
                await HandleRenewalCallbackAsync(parts, chatId, ct);
                break;
        }
    }

    // ── Main menu ─────────────────────────────────────────────────────────────

    private async Task ShowMainMenuAsync(long chatId, CancellationToken ct)
    {
        var pending = (await _renewalService.GetPendingRequestsAsync()).Count();
        var pendingNote = pending > 0 ? $"\n\n🔔 <b>{pending} درخواست تمدید</b> در انتظار بررسی" : string.Empty;

        await SendAsync(chatId,
            $"🌟 <b>پنل سوپرادمین</b>\n\nبه داشبورد مدیریت پلتفرم خوش آمدید.{pendingNote}",
            BuildMainMenuKeyboard(), ct);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    private async Task ShowDashboardAsync(long chatId, CancellationToken ct)
    {
        var allTenants   = (await _uow.Tenants.GetAllAsync()).ToList();
        var active       = allTenants.Count(t => t.Status == TenantStatus.Active);
        var pending      = allTenants.Count(t => t.Status == TenantStatus.PendingSetup);
        var suspended    = allTenants.Count(t => t.Status == TenantStatus.Suspended);
        var expired      = allTenants.Count(t => t.Status == TenantStatus.Expired);
        var trial        = allTenants.Count(t => t.IsTrial);
        var expiringSoon = (await _uow.Tenants.GetExpiringTenantsAsync(7)).Count();
        var pendingRenew = (await _renewalService.GetPendingRequestsAsync()).Count();

        var html = "🌐 <b>داشبورد کل پلتفرم</b>\n\n" +
                   $"🏢 کل مشتریان: <b>{allTenants.Count}</b>\n" +
                   $"✅ فعال: <b>{active}</b>\n" +
                   $"🧪 آزمایشی: <b>{trial}</b>\n" +
                   $"⏳ در انتظار راه‌اندازی: <b>{pending}</b>\n" +
                   $"🚫 معلق: <b>{suspended}</b>\n" +
                   $"❌ منقضی: <b>{expired}</b>\n" +
                   $"⚠️ در حال انقضا (۷ روز): <b>{expiringSoon}</b>\n\n" +
                   (pendingRenew > 0 ? $"🔄 درخواست تمدید در انتظار: <b>{pendingRenew}</b>\n\n" : "") +
                   $"🕐 {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";

        await SendAsync(chatId, html, BuildMainMenuKeyboard(), ct);
    }

    // ── Tenants list ──────────────────────────────────────────────────────────

    private async Task ShowTenantsAsync(long chatId, int page, CancellationToken ct)
    {
        const int pageSize = 8;
        var all   = (await _uow.Tenants.GetAllAsync()).OrderByDescending(t => t.Id).ToList();
        var total = all.Count;
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        if (total == 0)
        {
            await SendAsync(chatId, "👥 هیچ مشتری‌ای ثبت نشده است.", BuildMainMenuKeyboard(), ct);
            return;
        }

        var html = $"👥 <b>مشتریان ({total} کل — صفحه {page})</b>";
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var t in paged)
        {
            var icon = t.Status switch
            {
                TenantStatus.Active       => t.IsTrial ? "🧪" : "✅",
                TenantStatus.PendingSetup => "⏳",
                TenantStatus.Suspended    => "🚫",
                TenantStatus.Expired      => "❌",
                _                         => "❓"
            };
            var label   = $"{icon} {t.TenantName}";
            var botUser = NormalizeBotUsername(t.BotUsername);
            if (botUser != null) label += $" (@{botUser})";
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(label, $"sa:tenant:{t.Id}") });
        }

        var nav = new List<InlineKeyboardButton>();
        if (page > 1)               nav.Add(InlineKeyboardButton.WithCallbackData("◀️ قبلی",  $"sa:tenants:page:{page - 1}"));
        if (page * pageSize < total) nav.Add(InlineKeyboardButton.WithCallbackData("بعدی ▶️", $"sa:tenants:page:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());

        await SendAsync(chatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    // ── Tenant detail / actions ───────────────────────────────────────────────

    private async Task HandleTenantCallbackAsync(string[] parts, long chatId, long telegramId, CancellationToken ct)
    {
        var idStr  = parts.ElementAtOrDefault(2);
        var action = parts.ElementAtOrDefault(3);

        if (idStr == "confirm" && long.TryParse(parts.ElementAtOrDefault(3), out var saId))
        {
            await FinalizeTenantCreationAsync(chatId, saId, ct);
            return;
        }
        if (idStr == "cancel" && long.TryParse(parts.ElementAtOrDefault(3), out var saId2))
        {
            ClearConversation(saId2);
            await SendAsync(chatId, "❌ افزودن مشتری لغو شد.", BuildMainMenuKeyboard(), ct);
            return;
        }

        if (!int.TryParse(idStr, out var tenantId)) return;
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) { await SendAsync(chatId, "❌ مشتری یافت نشد.", null, ct); return; }

        switch (action)
        {
            case "suspend":
                // Collect suspension reason first
                var convS = GetConversation(telegramId);
                convS.State = SuperAdminState.AwaitingSuspensionReason;
                convS.PendingSuspendTenantId = tenantId;
                SaveConversation(telegramId, convS);
                await SendAsync(chatId,
                    $"🚫 <b>تعلیق {tenant.TenantName}</b>\n\nدلیل تعلیق را وارد کنید:",
                    BuildCancelKeyboard(), ct);
                break;

            case "activate":
                tenant.Status        = TenantStatus.Active;
                tenant.IsActive      = true;
                tenant.SuspendedAt   = null;
                tenant.SuspendedReason = null;
                _uow.Tenants.Update(tenant);
                await _uow.SaveChangesAsync(ct);
                _logger.LogInformation("SuperAdmin activated tenant {Id}", tenantId);
                await SendAsync(chatId, $"✅ مشتری <b>{tenant.TenantName}</b> فعال شد.", null, ct);
                await ShowTenantDetailAsync(chatId, tenantId, ct);
                break;

            case "plans":
                await ShowPlanPickerForTenantAsync(chatId, tenantId, ct);
                break;

            case "notes":
                await ShowTenantNotesAsync(chatId, tenantId, 1, ct);
                break;

            case "webhook":
                await RetryWebhookAsync(chatId, tenantId, ct);
                break;

            case "diagnose":
                await ShowWebhookDiagnosticsAsync(chatId, tenantId, ct);
                break;

            case "technical":
                await ShowTechnicalDetailsAsync(chatId, tenantId, ct);
                break;

            default:
                await ShowTenantDetailAsync(chatId, tenantId, ct);
                break;
        }
    }

    private async Task ShowTenantDetailAsync(long chatId, int tenantId, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) return;

        var notes  = (await _uow.TenantNotes.GetByTenantIdAsync(tenantId)).ToList();
        var health = _botHealth.GetStatus(tenantId);
        var botUser = NormalizeBotUsername(tenant.BotUsername);

        // ── expiry line ──
        string expiryLine;
        if (tenant.ExpiresAt.HasValue)
        {
            var daysLeft = (int)(tenant.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
            var urgency  = daysLeft <= 3 ? "‼️" : daysLeft <= 7 ? "⚠️" : "⏰";
            expiryLine = $"{urgency} انقضا: <b>{tenant.ExpiresAt.Value:yyyy-MM-dd}</b> ({daysLeft} روز)";
        }
        else
        {
            expiryLine = "⏰ انقضا: ♾ بدون انقضا";
        }

        // ── health / webhook line ──
        string healthLine;
        if (health is null)
        {
            healthLine = "📶 سلامت: ⏳ هنوز بررسی نشده";
        }
        else if (!health.IsOnline)
        {
            healthLine = "📶 سلامت: 🔴 آفلاین (توکن نامعتبر یا تلگرام در دسترس نیست)";
        }
        else if (health.WebhookChecked && !string.IsNullOrEmpty(health.WebhookLastError))
        {
            healthLine = $"📶 سلامت: 🟡 آنلاین ولی وب‌هوک خطا دارد\n" +
                         $"   ❗ <i>{health.WebhookLastError}</i>";
        }
        else if (health.WebhookChecked && string.IsNullOrEmpty(health.WebhookUrl))
        {
            healthLine = "📶 سلامت: 🟡 آنلاین ولی وب‌هوک ثبت نشده";
        }
        else
        {
            healthLine = $"📶 سلامت: 🟢 آنلاین{(health.PendingUpdateCount > 0 ? $" ({health.PendingUpdateCount} آپدیت در صف)" : "")}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🏢 <b>{tenant.TenantName}</b>{(tenant.IsTrial ? "  🧪 <i>آزمایشی</i>" : "")}");
        sb.AppendLine($"📊 وضعیت: {GetStatusLabel(tenant.Status)}");
        if (botUser != null) sb.AppendLine($"🤖 ربات: @{botUser}");
        sb.AppendLine(healthLine);
        sb.AppendLine();
        if (tenant.CustomerName  != null) sb.AppendLine($"👤 مشتری: {tenant.CustomerName}");
        if (tenant.CustomerPhone != null) sb.AppendLine($"📞 تلفن: {tenant.CustomerPhone}");
        if (tenant.CustomerEmail != null) sb.AppendLine($"📧 ایمیل: {tenant.CustomerEmail}");
        sb.AppendLine();
        sb.AppendLine(expiryLine);
        sb.AppendLine($"📦 محدودیت‌ها: {tenant.MaxUsers} کاربر | {tenant.MaxProducts} محصول | {tenant.MaxAdmins} ادمین");
        if (notes.Count > 0) sb.AppendLine($"📝 یادداشت: {notes.Count} مورد");
        if (tenant.Status == TenantStatus.Suspended && tenant.SuspendedReason != null)
        {
            sb.AppendLine();
            sb.AppendLine($"🚫 دلیل تعلیق: <i>{tenant.SuspendedReason}</i>");
            if (tenant.SuspendedAt.HasValue)
                sb.AppendLine($"📅 تعلیق در: {tenant.SuspendedAt.Value:yyyy-MM-dd HH:mm}");
        }

        var rows = new List<InlineKeyboardButton[]>();

        // ── status actions ──
        var actionRow = new List<InlineKeyboardButton>();
        if (tenant.Status != TenantStatus.Suspended && tenant.Status != TenantStatus.Disabled)
            actionRow.Add(InlineKeyboardButton.WithCallbackData("🚫 تعلیق", $"sa:tenant:{tenantId}:suspend"));
        if (tenant.Status != TenantStatus.Active)
            actionRow.Add(InlineKeyboardButton.WithCallbackData("✅ فعال‌سازی", $"sa:tenant:{tenantId}:activate"));
        if (actionRow.Count > 0) rows.Add(actionRow.ToArray());

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("📋 تغییر پلن",   $"sa:tenant:{tenantId}:plans"),
            InlineKeyboardButton.WithCallbackData("📝 یادداشت‌ها", $"sa:tenant:{tenantId}:notes")
        });
        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔄 تنظیم وب‌هوک", $"sa:tenant:{tenantId}:webhook"),
            InlineKeyboardButton.WithCallbackData("🔍 بررسی وبهوک",  $"sa:tenant:{tenantId}:diagnose")
        });

        // ── open / test bot buttons (URL buttons, only if username known) ──
        if (botUser != null)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithUrl("🤖 باز کردن ربات مشتری", $"https://t.me/{botUser}"),
                InlineKeyboardButton.WithUrl("🧪 تست ربات مشتری",       $"https://t.me/{botUser}?start=test")
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔧 جزئیات فنی", $"sa:tenant:{tenantId}:technical"),
            InlineKeyboardButton.WithCallbackData("⬅️ بازگشت",     "sa:tenants:page:1")
        });

        await SendAsync(chatId, sb.ToString(), new InlineKeyboardMarkup(rows), ct);
    }

    private async Task ShowTechnicalDetailsAsync(long chatId, int tenantId, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) return;

        var expectedUrl = !string.IsNullOrWhiteSpace(_opts.WebhookBaseUrl)
            ? $"{_opts.WebhookBaseUrl.TrimEnd('/')}/api/telegram/{tenant.TenantSlug}/webhook"
            : "(WebhookBaseUrl تنظیم نشده)";

        var sb = new StringBuilder();
        sb.AppendLine($"🔧 <b>جزئیات فنی — {tenant.TenantName}</b>");
        sb.AppendLine();
        sb.AppendLine($"🔗 Slug: <code>{tenant.TenantSlug}</code>");
        sb.AppendLine($"🆔 شناسه: #{tenant.Id}");
        sb.AppendLine($"🔑 IsActive: {(tenant.IsActive ? "✅ فعال" : "❌ غیرفعال")}");
        sb.AppendLine($"🔐 Secret وب‌هوک: {(string.IsNullOrEmpty(tenant.WebhookSecret) ? "❌ تنظیم نشده" : "✅ تنظیم شده")}");
        sb.AppendLine($"📡 آدرس وب‌هوک مورد انتظار:");
        sb.AppendLine($"<code>{expectedUrl}</code>");
        sb.AppendLine($"🗓 تاریخ ایجاد: {tenant.CreatedAt:yyyy-MM-dd HH:mm}");

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("⬅️ بازگشت به مشتری", $"sa:tenant:{tenantId}") }
        });
        await SendAsync(chatId, sb.ToString(), kb, ct);
    }

    // ── Webhook Diagnostics ───────────────────────────────────────────────────

    private async Task ShowWebhookDiagnosticsAsync(long chatId, int tenantId, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) { await SendAsync(chatId, "❌ مشتری یافت نشد.", null, ct); return; }

        await SendAsync(chatId, "🔍 در حال بررسی وب‌هوک...", null, ct);

        var botUser    = NormalizeBotUsername(tenant.BotUsername);
        var expectedUrl = !string.IsNullOrWhiteSpace(_opts.WebhookBaseUrl)
            ? $"{_opts.WebhookBaseUrl.TrimEnd('/')}/api/telegram/{tenant.TenantSlug}/webhook"
            : null;

        string? actualUrl       = null;
        string? lastError       = null;
        int     pendingCount    = 0;
        bool    tokenValid      = false;
        string? tokenError      = null;
        string? fetchedUsername = botUser;

        try
        {
            var decryptedToken = _aes.Decrypt(tenant.BotTokenEncrypted);
            var client         = new TelegramBotClient(decryptedToken);

            var me = await client.GetMe(ct);
            tokenValid      = true;
            fetchedUsername = me.Username;

            var webhookInfo = await client.GetWebhookInfo(ct);
            actualUrl    = webhookInfo.Url;
            lastError    = webhookInfo.LastErrorMessage;
            pendingCount = webhookInfo.PendingUpdateCount;
        }
        catch (Exception ex)
        {
            tokenError = ex.Message;
        }

        var urlMatch = expectedUrl != null && actualUrl != null &&
                       string.Equals(expectedUrl, actualUrl, StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("🔍 <b>گزارش تشخیص وب‌هوک</b>");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine($"🏢 فروشگاه: <b>{tenant.TenantName}</b>");
        sb.AppendLine($"📊 وضعیت دیتابیس: {GetStatusLabel(tenant.Status)}");
        sb.AppendLine($"🔑 IsActive: {(tenant.IsActive ? "✅ فعال" : "❌ غیرفعال — ربات پاسخ نمی‌دهد!")}");
        if (fetchedUsername != null) sb.AppendLine($"🤖 ربات: @{fetchedUsername}");
        sb.AppendLine();

        // token check
        sb.AppendLine(tokenValid
            ? "🔐 توکن ربات: ✅ معتبر"
            : $"🔐 توکن ربات: ❌ خطا — <i>{tokenError}</i>");
        sb.AppendLine();

        // webhook URL comparison
        sb.AppendLine("📡 <b>آدرس وب‌هوک:</b>");
        sb.AppendLine($"• مورد انتظار:");
        sb.AppendLine($"  <code>{expectedUrl ?? "(WebhookBaseUrl تنظیم نشده)"}</code>");
        sb.AppendLine($"• ثبت‌شده در تلگرام:");
        sb.AppendLine($"  <code>{(string.IsNullOrEmpty(actualUrl) ? "(ثبت نشده)" : actualUrl)}</code>");

        if (expectedUrl is null)
            sb.AppendLine("⚠️ نتیجه: WebhookBaseUrl در تنظیمات سرور خالی است");
        else if (string.IsNullOrEmpty(actualUrl))
            sb.AppendLine("❌ نتیجه: وب‌هوک در تلگرام ثبت نشده — دکمه «تنظیم وب‌هوک» را بزنید");
        else if (urlMatch)
            sb.AppendLine("✅ نتیجه: آدرس‌ها یکسان هستند");
        else
            sb.AppendLine("❌ نتیجه: آدرس‌ها مغایرت دارند — دکمه «تنظیم وب‌هوک» را بزنید");

        sb.AppendLine();
        sb.AppendLine($"🔐 Secret: {(string.IsNullOrEmpty(tenant.WebhookSecret) ? "❌ تنظیم نشده" : "✅ تنظیم شده")}");
        sb.AppendLine($"⏳ آپدیت‌های در صف: {pendingCount}");

        if (!string.IsNullOrEmpty(lastError))
        {
            sb.AppendLine();
            sb.AppendLine($"❗ آخرین خطای تلگرام:");
            sb.AppendLine($"<i>{lastError}</i>");
        }

        // Recommendation
        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
        if (!tenant.IsActive)
            sb.AppendLine("🔧 <b>راه‌حل:</b> دکمه «تنظیم وب‌هوک» را بزنید تا IsActive هم فعال شود.");
        else if (!tokenValid)
            sb.AppendLine("🔧 <b>راه‌حل:</b> توکن ربات نامعتبر است. از پنل مشتری توکن را بررسی کنید.");
        else if (string.IsNullOrEmpty(actualUrl) || !urlMatch)
            sb.AppendLine("🔧 <b>راه‌حل:</b> دکمه «تنظیم وب‌هوک» را بزنید.");
        else if (!string.IsNullOrEmpty(lastError))
            sb.AppendLine("🔧 <b>راه‌حل:</b> خطای تلگرام را بررسی کنید. احتمالاً SSL یا DNS مشکل دارد.");
        else
            sb.AppendLine("✅ <b>همه چیز درست است.</b> اگر ربات هنوز جواب نمی‌دهد، چند دقیقه صبر کنید.");

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 تنظیم مجدد وب‌هوک", $"sa:tenant:{tenantId}:webhook"),
                InlineKeyboardButton.WithCallbackData("⬅️ بازگشت",             $"sa:tenant:{tenantId}")
            }
        });

        await SendAsync(chatId, sb.ToString(), kb, ct);
    }

    // ── Plans ─────────────────────────────────────────────────────────────────

    private async Task ShowPlansAsync(long chatId, CancellationToken ct)
    {
        var plans = (await _uow.SubscriptionPlans.GetAllAsync()).ToList();
        if (plans.Count == 0)
        {
            await SendAsync(chatId, "📋 هیچ پلنی تعریف نشده است.", BuildMainMenuKeyboard(), ct);
            return;
        }

        var rows = plans.Select(p => new[]
        {
            InlineKeyboardButton.WithCallbackData(
                $"{p.Name} — {(p.MonthlyPrice > 0 ? $"{p.MonthlyPrice:F0} تومان/ماه" : "رایگان")}",
                $"sa:plan:{p.Id}")
        }).ToList();

        await SendAsync(chatId, "📋 <b>پلن‌های اشتراک</b>", new InlineKeyboardMarkup(rows), ct);
    }

    private async Task ShowPlanDetailAsync(long chatId, int planId, CancellationToken ct)
    {
        var plan = await _uow.SubscriptionPlans.GetByIdAsync(planId);
        if (plan is null) { await SendAsync(chatId, "❌ پلن یافت نشد.", null, ct); return; }

        var html = $"📋 <b>پلن: {plan.Name}</b> ({plan.Tier})\n\n" +
                   $"💰 ماهانه: {(plan.MonthlyPrice > 0 ? $"{plan.MonthlyPrice:F0} تومان" : "رایگان")}\n" +
                   $"💰 سالانه: {(plan.YearlyPrice  > 0 ? $"{plan.YearlyPrice:F0} تومان"  : "رایگان")}\n\n" +
                   $"👥 حداکثر کاربر: {plan.MaxUsers}\n" +
                   $"📦 حداکثر محصول: {plan.MaxProducts}\n" +
                   $"👑 حداکثر ادمین: {plan.MaxAdmins}\n" +
                   $"📈 سفارش/ماه: {plan.MaxOrdersPerMonth}\n\n" +
                   $"🤝 افیلیت: {B(plan.AllowsAffiliate)}\n" +
                   $"🎟 کوپن: {B(plan.AllowsCoupons)}\n" +
                   $"🤖 پشتیبانی AI: {B(plan.AllowsAiSupport)}\n" +
                   $"🏷 برندینگ اختصاصی: {B(plan.AllowsWhiteLabel)}\n" +
                   $"🌍 چندزبانه: {B(plan.AllowsMultiLanguage)}";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("⬅️ بازگشت", "sa:tenants:page:1") }
        });
        await SendAsync(chatId, html, kb, ct);
    }

    private async Task ShowPlanPickerForTenantAsync(long chatId, int tenantId, CancellationToken ct)
    {
        var plans  = (await _uow.SubscriptionPlans.GetAllAsync()).ToList();
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);

        var rows = plans.Select(p => new[]
        {
            InlineKeyboardButton.WithCallbackData(
                $"{(tenant?.PlanId == p.Id ? "✅ " : "")}{p.Name}",
                $"sa:setplan:{tenantId}:{p.Id}")
        }).ToList();
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ بازگشت", $"sa:tenant:{tenantId}") });

        await SendAsync(chatId, "📋 پلن مورد نظر را انتخاب کنید:", new InlineKeyboardMarkup(rows), ct);
    }

    private async Task HandleSetPlanCallbackAsync(string[] parts, long chatId, CancellationToken ct)
    {
        if (!int.TryParse(parts.ElementAtOrDefault(2), out var tenantId)) return;
        if (!int.TryParse(parts.ElementAtOrDefault(3), out var planId))   return;

        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        var plan   = await _uow.SubscriptionPlans.GetByIdAsync(planId);
        if (tenant is null || plan is null) return;

        tenant.PlanId              = planId;
        tenant.MaxUsers            = plan.MaxUsers;
        tenant.MaxProducts         = plan.MaxProducts;
        tenant.MaxAdmins           = plan.MaxAdmins;
        tenant.MaxOrdersPerMonth   = plan.MaxOrdersPerMonth;
        _uow.Tenants.Update(tenant);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SuperAdmin set plan {Plan} for tenant {Id}", plan.Name, tenantId);
        await SendAsync(chatId, $"✅ پلن <b>{plan.Name}</b> برای <b>{tenant.TenantName}</b> تنظیم شد.", null, ct);
        await ShowTenantDetailAsync(chatId, tenantId, ct);
    }

    // ── Add-tenant wizard (7-step) ─────────────────────────────────────────────

    private async Task StartAddTenantWizardAsync(long chatId, long telegramId, CancellationToken ct)
    {
        SaveConversation(telegramId, new SuperAdminConversation { State = SuperAdminState.AwaitingTenantName });
        await SendAsync(chatId,
            "➕ <b>افزودن مشتری جدید</b>\n\n<b>مرحله ۱ از ۷</b>\n📝 نام فروشگاه را وارد کنید:",
            BuildCancelKeyboard(), ct);
    }

    private async Task HandleWizardStepAsync(
        long chatId, long telegramId, string text, Message message,
        SuperAdminConversation conv, CancellationToken ct)
    {
        switch (conv.State)
        {
            case SuperAdminState.AwaitingTenantName:
                if (string.IsNullOrWhiteSpace(text)) { await SendAsync(chatId, "❌ نام نمی‌تواند خالی باشد.", null, ct); return; }
                conv.PendingTenantName = text.Trim();
                conv.State = SuperAdminState.AwaitingCustomerName;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    $"✅ نام فروشگاه: <b>{conv.PendingTenantName}</b>\n\n<b>مرحله ۲ از ۷</b>\n👤 نام مشتری را وارد کنید:",
                    BuildCancelKeyboard(), ct);
                break;

            case SuperAdminState.AwaitingCustomerName:
                if (string.IsNullOrWhiteSpace(text)) { await SendAsync(chatId, "❌ نام نمی‌تواند خالی باشد.", null, ct); return; }
                conv.PendingCustomerName = text.Trim();
                conv.State = SuperAdminState.AwaitingCustomerPhone;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    $"✅ نام مشتری: <b>{conv.PendingCustomerName}</b>\n\n<b>مرحله ۳ از ۷</b>\n📞 شماره تماس مشتری را وارد کنید:\n<i>(یا رد شوید)</i>",
                    BuildSkipCancelKeyboard(), ct);
                break;

            case SuperAdminState.AwaitingCustomerPhone:
                conv.PendingCustomerPhone = text.Trim();
                conv.State = SuperAdminState.AwaitingCustomerUsername;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    $"✅ تلفن: <b>{conv.PendingCustomerPhone}</b>\n\n<b>مرحله ۴ از ۷</b>\n👤 یوزرنیم تلگرام ادمین را وارد کنید:\n<i>(بدون @ — یا رد شوید)</i>",
                    BuildSkipCancelKeyboard(), ct);
                break;

            case SuperAdminState.AwaitingCustomerUsername:
                conv.PendingCustomerUsername = text.TrimStart('@').Trim();
                conv.State = SuperAdminState.AwaitingBotToken;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    $"✅ یوزرنیم: <b>@{conv.PendingCustomerUsername}</b>\n\n<b>مرحله ۵ از ۷</b>\n🤖 توکن بات تلگرام را وارد کنید:\n<i>(فرمت: 123456789:AAFxxxxxx)</i>",
                    BuildCancelKeyboard(), ct);
                break;

            case SuperAdminState.AwaitingBotToken:
                var token = text.Trim();
                if (!IsValidBotTokenFormat(token))
                {
                    await SendAsync(chatId,
                        "❌ فرمت توکن نامعتبر است.\nدوباره وارد کنید:", null, ct);
                    return;
                }
                conv.PendingBotToken = token;
                conv.State = SuperAdminState.AwaitingSubscriptionType;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    $"✅ توکن ذخیره شد.\n\n<b>مرحله ۶ از ۷</b>\nنوع اشتراک را انتخاب کنید:",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("💼 اشتراک پولی", $"sa:wizard:subtype:paid:{telegramId}"),
                            InlineKeyboardButton.WithCallbackData("🧪 آزمایشی رایگان", $"sa:wizard:subtype:trial:{telegramId}")
                        }
                    }), ct);
                break;

            case SuperAdminState.AwaitingSuspensionReason:
                if (string.IsNullOrWhiteSpace(text)) { await SendAsync(chatId, "❌ دلیل نمی‌تواند خالی باشد.", null, ct); return; }
                await ExecuteSuspensionAsync(chatId, conv.PendingSuspendTenantId, text.Trim(), ct);
                ClearConversation(telegramId);
                break;

            case SuperAdminState.AwaitingTenantNote:
                if (string.IsNullOrWhiteSpace(text)) { await SendAsync(chatId, "❌ یادداشت نمی‌تواند خالی باشد.", null, ct); return; }
                await AddTenantNoteAsync(chatId, telegramId, conv.PendingNoteTenantId, text.Trim(), ct);
                ClearConversation(telegramId);
                break;

            case SuperAdminState.AwaitingImpersonateTenantSlug:
                ClearConversation(telegramId);
                await HandleImpersonateInputAsync(chatId, text.Trim(), ct);
                break;
        }
    }

    // ── Wizard inline-keyboard callbacks ─────────────────────────────────────

    private async Task HandleWizardCallbackAsync(string[] parts, long chatId, long telegramId, CancellationToken ct)
    {
        var step  = parts.ElementAtOrDefault(2);
        var value = parts.ElementAtOrDefault(3);
        var saId  = long.TryParse(parts.ElementAtOrDefault(4), out var sid) ? sid : telegramId;

        var conv = GetConversation(saId);
        if (conv.State == SuperAdminState.None && step != "subtype")
        {
            await SendAsync(chatId, "❌ جلسه منقضی شده است. دوباره شروع کنید.", BuildMainMenuKeyboard(), ct);
            return;
        }

        switch (step)
        {
            case "subtype":
                if (value == "paid")
                {
                    conv.PendingIsTrial = false;
                    conv.State = SuperAdminState.AwaitingPlanSelection;
                    SaveConversation(saId, conv);

                    var plans = (await _uow.SubscriptionPlans.GetAllAsync()).Where(p => p.IsActive).ToList();
                    var rows  = plans.Select(p => new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            $"{p.Name} ({p.MonthlyPrice:F0} تومان/ماه)",
                            $"sa:wizard:plan:{p.Id}:{saId}")
                    }).ToArray();

                    await SendAsync(chatId,
                        "<b>مرحله ۶ از ۷</b>\nپلن اشتراک را انتخاب کنید:",
                        new InlineKeyboardMarkup(rows), ct);
                }
                else
                {
                    conv.PendingIsTrial = true;
                    conv.State = SuperAdminState.AwaitingTrialDuration;
                    SaveConversation(saId, conv);

                    await SendAsync(chatId,
                        "<b>مرحله ۶ از ۷</b>\nمدت آزمایشی را انتخاب کنید:",
                        new InlineKeyboardMarkup(new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("۷ روز",  $"sa:wizard:trial:7:{saId}"),
                                InlineKeyboardButton.WithCallbackData("۱۴ روز", $"sa:wizard:trial:14:{saId}"),
                                InlineKeyboardButton.WithCallbackData("۳۰ روز", $"sa:wizard:trial:30:{saId}")
                            }
                        }), ct);
                }
                break;

            case "plan":
                if (!int.TryParse(value, out var planId)) return;
                var plan = await _uow.SubscriptionPlans.GetByIdAsync(planId);
                if (plan is null) return;

                conv.PendingPlanId = planId;
                conv.State = SuperAdminState.AwaitingDurationSelection;
                SaveConversation(saId, conv);

                await SendAsync(chatId,
                    $"✅ پلن: <b>{plan.Name}</b>\n\n<b>مرحله ۷ از ۷</b>\nمدت اشتراک را انتخاب کنید:",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("۱ ماه",  $"sa:wizard:duration:1:{saId}"),
                            InlineKeyboardButton.WithCallbackData("۳ ماه",  $"sa:wizard:duration:3:{saId}")
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("۶ ماه",  $"sa:wizard:duration:6:{saId}"),
                            InlineKeyboardButton.WithCallbackData("۱۲ ماه", $"sa:wizard:duration:12:{saId}")
                        }
                    }), ct);
                break;

            case "duration":
                if (!int.TryParse(value, out var months)) return;
                conv.PendingDurationMonths = months;
                conv.State = SuperAdminState.ConfirmAddTenant;
                SaveConversation(saId, conv);
                await ShowWizardConfirmationAsync(chatId, saId, conv, ct);
                break;

            case "trial":
                if (!int.TryParse(value, out var days)) return;
                conv.PendingTrialDays = days;
                conv.State = SuperAdminState.ConfirmAddTenant;
                SaveConversation(saId, conv);
                await ShowWizardConfirmationAsync(chatId, saId, conv, ct);
                break;
        }
    }

    private async Task ShowWizardConfirmationAsync(long chatId, long saId, SuperAdminConversation conv, CancellationToken ct)
    {
        var slug = GenerateSlug(conv.PendingTenantName ?? "");
        var subInfo = conv.PendingIsTrial
            ? $"🧪 آزمایشی: <b>{conv.PendingTrialDays} روز</b>"
            : $"💼 پلن: <b>#{conv.PendingPlanId}</b> — <b>{conv.PendingDurationMonths} ماه</b>";

        var html = "📋 <b>تأیید اطلاعات مشتری جدید:</b>\n\n" +
                   $"🏢 فروشگاه: <b>{conv.PendingTenantName}</b>\n" +
                   $"🔗 Slug: <code>{slug}</code>\n" +
                   $"👤 مشتری: <b>{conv.PendingCustomerName}</b>\n" +
                   (conv.PendingCustomerPhone != null ? $"📞 تلفن: {conv.PendingCustomerPhone}\n" : "") +
                   (conv.PendingCustomerUsername != null ? $"👤 یوزرنیم: @{conv.PendingCustomerUsername}\n" : "") +
                   $"🤖 توکن: <code>{MaskToken(conv.PendingBotToken ?? "")}</code>\n" +
                   subInfo + "\n\n" +
                   "آیا اطلاعات را تأیید می‌کنید؟";

        await SendAsync(chatId, html,
            new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ تأیید", $"sa:tenant:confirm:{saId}"),
                    InlineKeyboardButton.WithCallbackData("❌ لغو",   $"sa:tenant:cancel:{saId}")
                }
            }), ct);
    }

    private async Task FinalizeTenantCreationAsync(long chatId, long saId, CancellationToken ct)
    {
        var conv = GetConversation(saId);
        if (conv.State != SuperAdminState.ConfirmAddTenant ||
            string.IsNullOrEmpty(conv.PendingTenantName) ||
            string.IsNullOrEmpty(conv.PendingBotToken))
        {
            ClearConversation(saId);
            await SendAsync(chatId, "❌ اطلاعات منقضی شده. دوباره امتحان کنید.", BuildMainMenuKeyboard(), ct);
            return;
        }

        ClearConversation(saId);
        await CreateTenantAsync(chatId, conv, ct);
    }

    private async Task CreateTenantAsync(long chatId, SuperAdminConversation conv, CancellationToken ct)
    {
        var slug           = await GenerateUniqueSlugAsync(conv.PendingTenantName!);
        var encryptedToken = _aes.Encrypt(conv.PendingBotToken!);
        var webhookSecret  = Guid.NewGuid().ToString("N")[..32];

        string? botUsername = null;
        var webhookSet = false;

        try
        {
            var newClient = new TelegramBotClient(conv.PendingBotToken!);
            var me = await newClient.GetMe(ct);
            botUsername = me.Username;

            if (!string.IsNullOrWhiteSpace(_opts.WebhookBaseUrl))
            {
                var webhookUrl = $"{_opts.WebhookBaseUrl.TrimEnd('/')}/api/telegram/{slug}/webhook";
                await newClient.SetWebhook(webhookUrl, secretToken: webhookSecret, cancellationToken: ct);
                webhookSet = true;
                _logger.LogInformation("Webhook set for new tenant {Slug}: {Url}", slug, webhookUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not auto-set webhook for tenant {Slug}", slug);
        }

        SubscriptionPlan? plan = null;
        if (!conv.PendingIsTrial && conv.PendingPlanId > 0)
            plan = await _uow.SubscriptionPlans.GetByIdAsync(conv.PendingPlanId);

        var expiresAt = conv.PendingIsTrial
            ? DateTime.UtcNow.AddDays(conv.PendingTrialDays)
            : DateTime.UtcNow.AddMonths(conv.PendingDurationMonths);

        var tenant = new Tenant
        {
            TenantName          = conv.PendingTenantName!,
            TenantSlug          = slug,
            CustomerName        = conv.PendingCustomerName,
            CustomerPhone       = conv.PendingCustomerPhone,
            BotTokenEncrypted   = encryptedToken,
            WebhookSecret       = webhookSecret,
            BotUsername         = botUsername,
            Status              = webhookSet ? TenantStatus.Active : TenantStatus.PendingSetup,
            IsActive            = true, // always active on creation; only suspension sets this false
            PlanId              = plan?.Id ?? 1,
            MaxUsers            = plan?.MaxUsers ?? 500,
            MaxProducts         = plan?.MaxProducts ?? 50,
            MaxAdmins           = plan?.MaxAdmins ?? 2,
            MaxOrdersPerMonth   = plan?.MaxOrdersPerMonth ?? 200,
            ExpiresAt           = expiresAt,
            TrialEndsAt         = conv.PendingIsTrial ? expiresAt : null,
            IsTrial             = conv.PendingIsTrial,
        };

        await _uow.Tenants.AddAsync(tenant);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SuperAdmin created tenant #{Id} ({Slug}) IsTrial={IsTrial}", tenant.Id, slug, conv.PendingIsTrial);

        var statusNote = webhookSet
            ? $"✅ وب‌هوک ثبت شد:\n<code>{_opts.WebhookBaseUrl?.TrimEnd('/')}/api/telegram/{slug}/webhook</code>"
            : "⚠️ وب‌هوک ثبت نشد — <code>Telegram:WebhookBaseUrl</code> را بررسی کنید.";

        var trialInfo = conv.PendingIsTrial
            ? $"🧪 آزمایشی: <b>{conv.PendingTrialDays} روز</b>\n⏰ انقضا: <b>{expiresAt:yyyy-MM-dd}</b>"
            : $"💼 پلن: <b>{plan?.Name ?? "پیش‌فرض"}</b> — <b>{conv.PendingDurationMonths} ماه</b>\n⏰ انقضا: <b>{expiresAt:yyyy-MM-dd}</b>";

        var html = "🎉 <b>مشتری جدید با موفقیت ثبت شد!</b>\n\n" +
                   $"🏢 فروشگاه: <b>{conv.PendingTenantName}</b>\n" +
                   $"👤 مشتری: <b>{conv.PendingCustomerName}</b>\n" +
                   $"🔗 Slug: <code>{slug}</code>\n" +
                   $"🆔 شناسه: #{tenant.Id}\n" +
                   (botUsername != null ? $"🤖 بات: @{botUsername}\n" : "") +
                   $"{trialInfo}\n\n" +
                   statusNote;

        await SendAsync(chatId, html, BuildMainMenuKeyboard(), ct);
    }

    // ── Suspension with reason ────────────────────────────────────────────────

    private async Task ExecuteSuspensionAsync(long chatId, int tenantId, string reason, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) { await SendAsync(chatId, "❌ مشتری یافت نشد.", BuildMainMenuKeyboard(), ct); return; }

        tenant.Status          = TenantStatus.Suspended;
        tenant.IsActive        = false;
        tenant.SuspendedReason = reason;
        tenant.SuspendedAt     = DateTime.UtcNow;
        _uow.Tenants.Update(tenant);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SuperAdmin suspended tenant {Id} — reason: {Reason}", tenantId, reason);
        await SendAsync(chatId, $"🚫 مشتری <b>{tenant.TenantName}</b> معلق شد.\n\nدلیل: <i>{reason}</i>", null, ct);
        await ShowTenantDetailAsync(chatId, tenantId, ct);
    }

    // ── Tenant Notes / CRM ────────────────────────────────────────────────────

    private async Task HandleNotesCallbackAsync(string[] parts, long chatId, long telegramId, CancellationToken ct)
    {
        var action   = parts.ElementAtOrDefault(2);
        var tenantId = int.TryParse(parts.ElementAtOrDefault(3), out var tid) ? tid : 0;
        var noteId   = int.TryParse(parts.ElementAtOrDefault(4), out var nid) ? nid : 0;

        switch (action)
        {
            case var _ when int.TryParse(action, out var page) && tenantId == 0:
                // sa:notes:{tenantId} — show notes for tenantId stored in action slot
                await ShowTenantNotesAsync(chatId, page, 1, ct);
                break;

            case "add":
                var conv = GetConversation(telegramId);
                conv.State = SuperAdminState.AwaitingTenantNote;
                conv.PendingNoteTenantId = tenantId;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId, "📝 یادداشت جدید را وارد کنید:", BuildCancelKeyboard(), ct);
                break;

            case "del":
                await DeleteTenantNoteAsync(chatId, tenantId, noteId, ct);
                break;

            case "page":
                var pg = int.TryParse(parts.ElementAtOrDefault(4), out var p) ? p : 1;
                await ShowTenantNotesAsync(chatId, tenantId, pg, ct);
                break;
        }
    }

    private async Task ShowTenantNotesAsync(long chatId, int tenantId, int page, CancellationToken ct)
    {
        const int pageSize = 5;
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) return;

        var notes = (await _uow.TenantNotes.GetByTenantIdAsync(tenantId)).ToList();
        var paged = notes.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"📝 <b>یادداشت‌های {tenant.TenantName}</b> ({notes.Count} مورد)");
        sb.AppendLine();

        var rows = new List<InlineKeyboardButton[]>();

        foreach (var note in paged)
        {
            sb.AppendLine($"• {note.Note}");
            sb.AppendLine($"  <i>{note.CreatedAt:yyyy-MM-dd HH:mm}</i>");
            sb.AppendLine();
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"🗑 حذف ({note.CreatedAt:MM/dd})", $"sa:notes:del:{tenantId}:{note.Id}")
            });
        }

        if (notes.Count == 0) sb.AppendLine("هیچ یادداشتی ثبت نشده است.");

        var nav = new List<InlineKeyboardButton>();
        if (page > 1)                    nav.Add(InlineKeyboardButton.WithCallbackData("◀️", $"sa:notes:page:{tenantId}:{page - 1}"));
        if (page * pageSize < notes.Count) nav.Add(InlineKeyboardButton.WithCallbackData("▶️", $"sa:notes:page:{tenantId}:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("➕ افزودن یادداشت", $"sa:notes:add:{tenantId}"),
            InlineKeyboardButton.WithCallbackData("⬅️ بازگشت",         $"sa:tenant:{tenantId}")
        });

        await SendAsync(chatId, sb.ToString(), new InlineKeyboardMarkup(rows), ct);
    }

    private async Task AddTenantNoteAsync(long chatId, long superAdminId, int tenantId, string note, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) { await SendAsync(chatId, "❌ مشتری یافت نشد.", BuildMainMenuKeyboard(), ct); return; }

        var newNote = new TenantNote
        {
            TenantId               = tenantId,
            Note                   = note,
            CreatedBySuperAdminId  = superAdminId
        };
        await _uow.TenantNotes.AddAsync(newNote);
        await _uow.SaveChangesAsync(ct);

        await SendAsync(chatId, $"✅ یادداشت برای <b>{tenant.TenantName}</b> ثبت شد.", null, ct);
        await ShowTenantNotesAsync(chatId, tenantId, 1, ct);
    }

    private async Task DeleteTenantNoteAsync(long chatId, int tenantId, int noteId, CancellationToken ct)
    {
        var note = await _uow.TenantNotes.GetByIdAsync(noteId);
        if (note is null) { await SendAsync(chatId, "❌ یادداشت یافت نشد.", null, ct); return; }

        _uow.TenantNotes.Remove(note);
        await _uow.SaveChangesAsync(ct);

        await SendAsync(chatId, "🗑 یادداشت حذف شد.", null, ct);
        await ShowTenantNotesAsync(chatId, tenantId, 1, ct);
    }

    // ── Health Monitoring ─────────────────────────────────────────────────────

    private async Task ShowHealthAsync(long chatId, CancellationToken ct)
    {
        var allStatuses   = _botHealth.GetAllStatuses();
        var online        = allStatuses.Count(s => s.IsOnline);
        var offline       = allStatuses.Count(s => !s.IsOnline);
        var webhookIssues = allStatuses.Count(s => s.IsOnline && s.WebhookChecked &&
                                                   (!string.IsNullOrEmpty(s.WebhookLastError) || string.IsNullOrEmpty(s.WebhookUrl)));

        var sb = new StringBuilder();
        sb.AppendLine("📡 <b>سلامت سیستم</b>\n");
        sb.AppendLine($"🟢 آنلاین: <b>{online}</b>  🔴 آفلاین: <b>{offline}</b>" +
                      (webhookIssues > 0 ? $"  🟡 مشکل وب‌هوک: <b>{webhookIssues}</b>" : ""));
        sb.AppendLine($"📊 کل بررسی‌شده: {allStatuses.Count}");
        sb.AppendLine();

        if (allStatuses.Count > 0)
        {
            sb.AppendLine("<b>وضعیت بات‌ها:</b>");
            foreach (var s in allStatuses.Take(15))
            {
                string icon;
                string extra = string.Empty;
                if (!s.IsOnline)
                {
                    icon = "🔴";
                }
                else if (s.WebhookChecked && !string.IsNullOrEmpty(s.WebhookLastError))
                {
                    icon  = "🟡";
                    extra = $" ❗ <i>{s.WebhookLastError[..Math.Min(50, s.WebhookLastError.Length)]}</i>";
                }
                else if (s.WebhookChecked && string.IsNullOrEmpty(s.WebhookUrl))
                {
                    icon  = "🟡";
                    extra = " ⚠️ <i>وب‌هوک ثبت نشده</i>";
                }
                else
                {
                    icon = "🟢";
                    if (s.PendingUpdateCount > 5) extra = $" ({s.PendingUpdateCount} در صف)";
                }

                var uname = NormalizeBotUsername(s.BotUsername);
                sb.AppendLine($"{icon} {s.TenantName}" +
                              (uname != null ? $" (@{uname})" : "") +
                              $" — <i>{s.CheckedAt:HH:mm}</i>{extra}");
            }
            if (allStatuses.Count > 15)
                sb.AppendLine($"<i>... و {allStatuses.Count - 15} مورد دیگر</i>");
        }
        else
        {
            sb.AppendLine("<i>هنوز بررسی انجام نشده است. چند دقیقه صبر کنید.</i>");
        }

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔄 بروزرسانی", "sa:health:refresh") }
        };

        // Quick-fix buttons for offline or webhook-broken bots
        var needsAttention = allStatuses
            .Where(s => !s.IsOnline || (s.WebhookChecked && (!string.IsNullOrEmpty(s.WebhookLastError) || string.IsNullOrEmpty(s.WebhookUrl))))
            .Take(5);
        foreach (var s in needsAttention)
        {
            var uname = NormalizeBotUsername(s.BotUsername) ?? s.TenantName;
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData($"🔄 تنظیم وب‌هوک @{uname}", $"sa:health:retry:{s.TenantId}") });
        }

        await SendAsync(chatId, sb.ToString(), new InlineKeyboardMarkup(rows), ct);
    }

    private async Task HandleHealthCallbackAsync(string[] parts, long chatId, CancellationToken ct)
    {
        var action = parts.ElementAtOrDefault(2);

        switch (action)
        {
            case "refresh":
                await ShowHealthAsync(chatId, ct);
                break;

            case "retry" when int.TryParse(parts.ElementAtOrDefault(3), out var tenantId):
                await RetryWebhookAsync(chatId, tenantId, ct);
                break;
        }
    }

    private async Task RetryWebhookAsync(long chatId, int tenantId, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null) { await SendAsync(chatId, "❌ مشتری یافت نشد.", null, ct); return; }
        if (string.IsNullOrWhiteSpace(_opts.WebhookBaseUrl))
        {
            await SendAsync(chatId, "❌ مقدار <code>Telegram:WebhookBaseUrl</code> تنظیم نشده است.", null, ct);
            return;
        }

        try
        {
            var decryptedToken = _aes.Decrypt(tenant.BotTokenEncrypted);
            var client = new TelegramBotClient(decryptedToken);
            var webhookUrl = $"{_opts.WebhookBaseUrl.TrimEnd('/')}/api/telegram/{tenant.TenantSlug}/webhook";
            await client.SetWebhook(webhookUrl, secretToken: tenant.WebhookSecret, cancellationToken: ct);

            // Ensure tenant is active after a successful webhook setup regardless of prior status
            var changed = false;
            if (tenant.Status == TenantStatus.PendingSetup) { tenant.Status = TenantStatus.Active; changed = true; }
            if (!tenant.IsActive) { tenant.IsActive = true; changed = true; }
            if (changed)
            {
                _uow.Tenants.Update(tenant);
                await _uow.SaveChangesAsync(ct);
            }

            _logger.LogInformation("SuperAdmin retried webhook for tenant {Id}", tenantId);
            await SendAsync(chatId, $"✅ وب‌هوک برای <b>{tenant.TenantName}</b> مجدداً تنظیم شد.", null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook retry failed for tenant {Id}", tenantId);
            await SendAsync(chatId, $"❌ تنظیم وب‌هوک ناموفق بود:\n<code>{ex.Message}</code>", null, ct);
        }
    }

    // ── Backup Center ─────────────────────────────────────────────────────────

    private async Task ShowBackupMenuAsync(long chatId, CancellationToken ct)
    {
        var backups = await _backupService.ListBackupsAsync();
        var html = $"💾 <b>مرکز پشتیبان‌گیری</b>\n\n" +
                   $"📦 تعداد فایل‌های موجود: <b>{backups.Count}</b>";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🆕 ایجاد بکاپ الان", "sa:backup:trigger") },
            new[] { InlineKeyboardButton.WithCallbackData("📋 مشاهده بکاپ‌ها",  "sa:backup:list:1") }
        });

        await SendAsync(chatId, html, kb, ct);
    }

    private async Task HandleBackupCallbackAsync(string[] parts, long chatId, int msgId, CancellationToken ct)
    {
        var action = parts.ElementAtOrDefault(2);

        switch (action)
        {
            case "trigger":
                await SendAsync(chatId, "⏳ در حال ایجاد بکاپ... این ممکن است چند دقیقه طول بکشد.", null, ct);
                var result = await _backupService.TriggerBackupAsync(ct);
                if (result.IsSuccess)
                {
                    var info = result.Data!;
                    var sizeMb = info.SizeBytes / 1_048_576.0;
                    await SendAsync(chatId,
                        $"✅ بکاپ ایجاد شد!\n\n📄 فایل: <code>{info.FileName}</code>\n📦 حجم: <b>{sizeMb:F2} MB</b>",
                        null, ct);
                }
                else
                {
                    await SendAsync(chatId, $"❌ بکاپ ناموفق:\n{result.ErrorMessage}", null, ct);
                }
                break;

            case "list":
                var pg = int.TryParse(parts.ElementAtOrDefault(3), out var p) ? p : 1;
                await ShowBackupListAsync(chatId, pg, ct);
                break;

            case "download":
                var dlFile = parts.ElementAtOrDefault(3);
                await DownloadBackupAsync(chatId, dlFile, ct);
                break;

            case "delete":
                var delFile = parts.ElementAtOrDefault(3);
                await DeleteBackupAsync(chatId, delFile, ct);
                break;

            case "verify":
                var verFile = parts.ElementAtOrDefault(3);
                await VerifyBackupAsync(chatId, verFile, ct);
                break;
        }
    }

    private async Task ShowBackupListAsync(long chatId, int page, CancellationToken ct)
    {
        const int pageSize = 5;
        var all   = await _backupService.ListBackupsAsync();
        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        if (all.Count == 0)
        {
            await SendAsync(chatId, "💾 هیچ بکاپی یافت نشد.", null, ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"💾 <b>بکاپ‌ها ({all.Count} فایل — صفحه {page})</b>\n");

        var rows = new List<InlineKeyboardButton[]>();
        foreach (var b in paged)
        {
            var sizeMb = b.SizeBytes / 1_048_576.0;
            sb.AppendLine($"📄 <code>{b.FileName}</code>");
            sb.AppendLine($"   📦 {sizeMb:F1} MB | 🗓 {b.CreatedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine();

            var safeFile = Uri.EscapeDataString(b.FileName);
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("⬇️ دانلود", $"sa:backup:download:{safeFile}"),
                InlineKeyboardButton.WithCallbackData("✔️ تأیید",  $"sa:backup:verify:{safeFile}"),
                InlineKeyboardButton.WithCallbackData("🗑 حذف",    $"sa:backup:delete:{safeFile}")
            });
        }

        var nav = new List<InlineKeyboardButton>();
        if (page > 1)                    nav.Add(InlineKeyboardButton.WithCallbackData("◀️", $"sa:backup:list:{page - 1}"));
        if (page * pageSize < all.Count) nav.Add(InlineKeyboardButton.WithCallbackData("▶️", $"sa:backup:list:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());

        await SendAsync(chatId, sb.ToString(), new InlineKeyboardMarkup(rows), ct);
    }

    private async Task DownloadBackupAsync(long chatId, string? fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        var decodedFile = Uri.UnescapeDataString(fileName);
        var path = await _backupService.GetBackupFullPathAsync(decodedFile);
        if (path is null)
        {
            await SendAsync(chatId, "❌ فایل بکاپ یافت نشد.", null, ct);
            return;
        }

        var fi = new FileInfo(path);
        if (fi.Length > 50 * 1024 * 1024)
        {
            await SendAsync(chatId,
                $"⚠️ فایل بکاپ <b>{fi.Length / 1_048_576.0:F1} MB</b> است که از محدودیت ۵۰ مگابایت تلگرام بیشتر است.\n\nلطفاً مستقیماً از سرور دریافت کنید.",
                null, ct);
            return;
        }

        try
        {
            await using var stream = System.IO.File.OpenRead(path);
            var inputFile = InputFile.FromStream(stream, fi.Name);
            await _bot.SendDocument(chatId, inputFile,
                caption: $"💾 {fi.Name}\n{fi.Length / 1_048_576.0:F2} MB",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send backup file {File}", decodedFile);
            await SendAsync(chatId, $"❌ ارسال فایل ناموفق بود: {ex.Message}", null, ct);
        }
    }

    private async Task DeleteBackupAsync(long chatId, string? fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        var decodedFile = Uri.UnescapeDataString(fileName);
        var result = await _backupService.DeleteBackupAsync(decodedFile);
        await SendAsync(chatId,
            result.IsSuccess ? $"🗑 فایل <code>{decodedFile}</code> حذف شد." : $"❌ {result.ErrorMessage}",
            null, ct);
    }

    private async Task VerifyBackupAsync(long chatId, string? fileName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;
        await SendAsync(chatId, "⏳ در حال تأیید صحت بکاپ...", null, ct);
        var decodedFile = Uri.UnescapeDataString(fileName);
        var result = await _backupService.VerifyBackupAsync(decodedFile);
        await SendAsync(chatId,
            result.IsSuccess
                ? $"✅ بکاپ <code>{decodedFile}</code> سالم است."
                : $"❌ بکاپ معتبر نیست:\n{result.ErrorMessage}",
            null, ct);
    }

    // ── Renewal Requests ──────────────────────────────────────────────────────

    private async Task ShowPendingRenewalsAsync(long chatId, CancellationToken ct)
    {
        var pending = (await _renewalService.GetPendingRequestsAsync()).ToList();

        if (pending.Count == 0)
        {
            await SendAsync(chatId, "🔄 هیچ درخواست تمدیدی در انتظار بررسی نیست.", BuildMainMenuKeyboard(), ct);
            return;
        }

        foreach (var req in pending.Take(10))
        {
            var tenantName = req.Tenant?.TenantName ?? $"#{req.TenantId}";
            var typeLabel  = req.RequestType == ECommerceBot.API.Enums.RenewalRequestType.Renewal
                ? $"تمدید {req.DurationMonths} ماهه" : "ارتقاء پلن";

            var html = $"🔄 <b>درخواست {typeLabel}</b>\n\n" +
                       $"🏢 فروشگاه: <b>{tenantName}</b>\n" +
                       $"📅 ارسال: {req.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                       (req.ReceiptFileId != null ? "🧾 رسید پیوست شده" : "⚠️ بدون رسید");

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ تأیید",  $"sa:renewal:approve:{req.Id}"),
                    InlineKeyboardButton.WithCallbackData("❌ رد",     $"sa:renewal:reject:{req.Id}")
                }
            });

            await SendAsync(chatId, html, kb, ct);

            if (req.ReceiptFileId != null)
            {
                try { await _bot.ForwardMessage(chatId, chatId, 0, cancellationToken: ct); }
                catch { /* receipt photo may not be forwardable this way */ }

                // Send the receipt photo directly
                try
                {
                    await _bot.SendPhoto(chatId, InputFile.FromFileId(req.ReceiptFileId), cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not send receipt photo for request {Id}", req.Id);
                }
            }
        }
    }

    private async Task HandleRenewalCallbackAsync(string[] parts, long chatId, CancellationToken ct)
    {
        var action    = parts.ElementAtOrDefault(2);
        var requestId = int.TryParse(parts.ElementAtOrDefault(3), out var rid) ? rid : 0;
        if (requestId == 0) return;

        switch (action)
        {
            case "approve":
                var approveResult = await _renewalService.ApproveAsync(requestId);
                var req = (await _renewalService.GetByTenantIdAsync(0)).FirstOrDefault(); // placeholder — get from repo
                await SendAsync(chatId,
                    approveResult.IsSuccess
                        ? $"✅ درخواست #{requestId} تأیید و اشتراک تمدید شد."
                        : $"❌ {approveResult.ErrorMessage}",
                    null, ct);

                // Notify tenant owner if we can find their chat ID
                var approvedReq = await _uow.RenewalRequests.GetByIdAsync(requestId);
                if (approvedReq is not null && approveResult.IsSuccess)
                    await NotifyTenantOwnerAsync(approvedReq.TenantId, "✅ درخواست تمدید اشتراک شما تأیید شد.", ct);
                break;

            case "reject":
                var rejectResult = await _renewalService.RejectAsync(requestId);
                await SendAsync(chatId,
                    rejectResult.IsSuccess
                        ? $"❌ درخواست #{requestId} رد شد."
                        : $"❌ {rejectResult.ErrorMessage}",
                    null, ct);

                var rejectedReq = await _uow.RenewalRequests.GetByIdAsync(requestId);
                if (rejectedReq is not null && rejectResult.IsSuccess)
                    await NotifyTenantOwnerAsync(rejectedReq.TenantId, "❌ درخواست تمدید اشتراک شما رد شد. لطفاً با پشتیبانی تماس بگیرید.", ct);
                break;
        }
    }

    private async Task NotifyTenantOwnerAsync(int tenantId, string message, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant?.OwnerTelegramId is null) return;

        try
        {
            var decryptedToken = _aes.Decrypt(tenant.BotTokenEncrypted);
            var tenantClient = new TelegramBotClient(decryptedToken);
            await tenantClient.SendMessage(tenant.OwnerTelegramId.Value, message,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify tenant owner for tenant {Id}", tenantId);
        }
    }

    // ── Impersonate ───────────────────────────────────────────────────────────

    private async Task StartImpersonateAsync(long chatId, long telegramId, CancellationToken ct)
    {
        SaveConversation(telegramId, new SuperAdminConversation { State = SuperAdminState.AwaitingImpersonateTenantSlug });
        await SendAsync(chatId,
            "🔑 <b>ورود به پنل مشتری</b>\n\nشناسه (slug) مشتری را وارد کنید:",
            BuildCancelKeyboard(), ct);
    }

    private async Task HandleImpersonateInputAsync(long chatId, string slug, CancellationToken ct)
    {
        var tenant = await _uow.Tenants.GetBySlugAsync(slug);
        if (tenant is null)
        {
            await SendAsync(chatId, $"❌ مشتری با شناسه <code>{slug}</code> یافت نشد.", BuildMainMenuKeyboard(), ct);
            return;
        }

        var html = $"🏢 <b>{tenant.TenantName}</b>\n\n" +
                   $"🔗 Slug: <code>{tenant.TenantSlug}</code>\n" +
                   $"📊 وضعیت: {GetStatusLabel(tenant.Status)}\n" +
                   (tenant.BotUsername != null ? $"🤖 بات: @{tenant.BotUsername}\n" : "") +
                   "\n<i>برای ورود به پنل ادمین این مشتری، از طریق بات آن‌ها /start بزنید.</i>";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📋 جزئیات کامل", $"sa:tenant:{tenant.Id}") }
        });
        await SendAsync(chatId, html, kb, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SendAsync(long chatId, string html, IReplyMarkup? markup, CancellationToken ct)
    {
        try
        {
            await _bot.SendMessage(chatId, html,
                parseMode: ParseMode.Html, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SuperAdmin: failed to send message to {ChatId}", chatId);
        }
    }

    private async Task AnswerCallbackAsync(string callbackId, CancellationToken ct)
    {
        try { await _bot.AnswerCallbackQuery(callbackId, cancellationToken: ct); }
        catch { /* ignore */ }
    }

    private static ReplyKeyboardMarkup BuildMainMenuKeyboard() =>
        new(new[]
        {
            new[] { new KeyboardButton(BtnDashboard),   new KeyboardButton(BtnTenants)   },
            new[] { new KeyboardButton(BtnAddTenant),   new KeyboardButton(BtnPlans)      },
            new[] { new KeyboardButton(BtnImpersonate), new KeyboardButton(BtnHealth)     },
            new[] { new KeyboardButton(BtnBackup),      new KeyboardButton(BtnRenewals)   }
        })
        { ResizeKeyboard = true };

    private static ReplyKeyboardMarkup BuildCancelKeyboard() =>
        new(BtnCancel) { ResizeKeyboard = true };

    private static ReplyKeyboardMarkup BuildSkipCancelKeyboard() =>
        new(new[]
        {
            new[] { new KeyboardButton(BtnSkip) },
            new[] { new KeyboardButton(BtnCancel) }
        })
        { ResizeKeyboard = true };

    private static string GetStatusLabel(TenantStatus status) => status switch
    {
        TenantStatus.Active       => "✅ فعال",
        TenantStatus.PendingSetup => "⏳ در انتظار راه‌اندازی",
        TenantStatus.Suspended    => "🚫 معلق",
        TenantStatus.Expired      => "❌ منقضی",
        TenantStatus.Disabled     => "🔒 غیرفعال",
        _                         => "❓ نامشخص"
    };

    private static bool IsValidBotTokenFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 30) return false;
        var colon = token.IndexOf(':');
        if (colon <= 0 || colon == token.Length - 1) return false;
        return token[..colon].All(char.IsDigit);
    }

    private static string MaskToken(string token)
    {
        var colon = token.IndexOf(':');
        if (colon < 0) return "***";
        var prefix = token[..(Math.Min(colon + 5, token.Length))];
        return prefix + "…***";
    }

    private static string GenerateSlug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLower())
        {
            if (c is >= 'a' and <= 'z' || c is >= '0' and <= '9')
                sb.Append(c);
            else
                sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        slug = Regex.Replace(slug, @"-{2,}", "-");
        if (slug.Length > 30) slug = slug[..30].TrimEnd('-');
        if (string.IsNullOrWhiteSpace(slug)) slug = $"tenant-{Random.Shared.Next(1000, 9999)}";
        return slug;
    }

    private async Task<string> GenerateUniqueSlugAsync(string name)
    {
        var baseSlug = GenerateSlug(name);
        var slug = baseSlug;
        for (var i = 1; i < 100; i++)
        {
            if (await _uow.Tenants.GetBySlugAsync(slug) is null) return slug;
            slug = $"{baseSlug}-{i}";
        }
        return Guid.NewGuid().ToString("N")[..8];
    }

    private static string B(bool v) => v ? "✅" : "❌";

    /// <summary>Strips any leading or trailing @ from a stored bot username so callers can safely prepend @ once.</summary>
    private static string? NormalizeBotUsername(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : username.Trim().Trim('@');

    // ── MemoryCache conversation state ────────────────────────────────────────

    private SuperAdminConversation GetConversation(long telegramId) =>
        _cache.TryGetValue(CacheKey(telegramId), out SuperAdminConversation? c) && c != null
            ? c
            : new SuperAdminConversation();

    private void SaveConversation(long telegramId, SuperAdminConversation conv) =>
        _cache.Set(CacheKey(telegramId), conv, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(1),
            Size = 1
        });

    private void ClearConversation(long telegramId) =>
        _cache.Remove(CacheKey(telegramId));

    private static string CacheKey(long telegramId) => $"sa_conv:{telegramId}";
}
