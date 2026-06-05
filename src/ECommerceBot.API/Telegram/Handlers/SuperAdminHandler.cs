using System.Text;
using System.Text.RegularExpressions;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Security;
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
    private readonly ILogger<SuperAdminHandler> _logger;

    // ── Hardcoded Persian menu labels ─────────────────────────────────────────
    private const string BtnDashboard   = "🌐 داشبورد کل";
    private const string BtnTenants     = "👥 مشتریان";
    private const string BtnAddTenant   = "➕ افزودن مشتری";
    private const string BtnPlans       = "📋 پلن‌ها";
    private const string BtnImpersonate = "🔑 ورود به پنل مشتری";
    private const string BtnCancel      = "❌ لغو";

    public SuperAdminHandler(
        IUnitOfWork uow,
        ITelegramBotClient bot,
        IAesEncryptionService aes,
        IMemoryCache cache,
        IOptions<TelegramOptions> opts,
        ILogger<SuperAdminHandler> logger)
    {
        _uow = uow;
        _bot = bot;
        _aes = aes;
        _cache = cache;
        _opts = opts.Value;
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

        var conv = GetConversation(telegramId);
        if (conv.State != SuperAdminState.None)
        {
            await HandleWizardStepAsync(chatId, telegramId, text, conv, ct);
            return;
        }

        switch (text)
        {
            case BtnDashboard:   await ShowDashboardAsync(chatId, ct); break;
            case BtnTenants:     await ShowTenantsAsync(chatId, 1, ct); break;
            case BtnAddTenant:   await StartAddTenantWizardAsync(chatId, telegramId, ct); break;
            case BtnPlans:       await ShowPlansAsync(chatId, ct); break;
            case BtnImpersonate: await StartImpersonateAsync(chatId, telegramId, ct); break;
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
        }
    }

    // ── Main menu ─────────────────────────────────────────────────────────────

    private async Task ShowMainMenuAsync(long chatId, CancellationToken ct)
    {
        await SendAsync(chatId,
            "🌟 <b>پنل سوپرادمین</b>\n\nبه داشبورد مدیریت پلتفرم خوش آمدید.",
            BuildMainMenuKeyboard(), ct);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    private async Task ShowDashboardAsync(long chatId, CancellationToken ct)
    {
        var allTenants    = (await _uow.Tenants.GetAllAsync()).ToList();
        var active        = allTenants.Count(t => t.Status == TenantStatus.Active);
        var pending       = allTenants.Count(t => t.Status == TenantStatus.PendingSetup);
        var suspended     = allTenants.Count(t => t.Status == TenantStatus.Suspended);
        var expired       = allTenants.Count(t => t.Status == TenantStatus.Expired);
        var expiringSoon  = (await _uow.Tenants.GetExpiringTenantsAsync(7)).Count();

        var html = "🌐 <b>داشبورد کل پلتفرم</b>\n\n" +
                   $"🏢 کل مشتریان: <b>{allTenants.Count}</b>\n" +
                   $"✅ فعال: <b>{active}</b>\n" +
                   $"⏳ در انتظار راه‌اندازی: <b>{pending}</b>\n" +
                   $"🚫 معلق: <b>{suspended}</b>\n" +
                   $"❌ منقضی: <b>{expired}</b>\n" +
                   $"⚠️ در حال انقضا (۷ روز): <b>{expiringSoon}</b>\n\n" +
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
                TenantStatus.Active       => "✅",
                TenantStatus.PendingSetup => "⏳",
                TenantStatus.Suspended    => "🚫",
                TenantStatus.Expired      => "❌",
                _                         => "❓"
            };
            var label = $"{icon} {t.TenantName}";
            if (t.BotUsername != null) label += $" (@{t.BotUsername})";
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(label, $"sa:tenant:{t.Id}") });
        }

        var nav = new List<InlineKeyboardButton>();
        if (page > 1)          nav.Add(InlineKeyboardButton.WithCallbackData("◀️ قبلی",  $"sa:tenants:page:{page - 1}"));
        if (page * pageSize < total) nav.Add(InlineKeyboardButton.WithCallbackData("بعدی ▶️", $"sa:tenants:page:{page + 1}"));
        if (nav.Count > 0) rows.Add(nav.ToArray());

        await SendAsync(chatId, html, new InlineKeyboardMarkup(rows), ct);
    }

    // ── Tenant detail / actions ───────────────────────────────────────────────

    private async Task HandleTenantCallbackAsync(string[] parts, long chatId, long telegramId, CancellationToken ct)
    {
        var idStr  = parts.ElementAtOrDefault(2);
        var action = parts.ElementAtOrDefault(3);

        // Confirm/cancel new tenant creation
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
                tenant.Status   = TenantStatus.Suspended;
                tenant.IsActive = false;
                _uow.Tenants.Update(tenant);
                await _uow.SaveChangesAsync(ct);
                _logger.LogInformation("SuperAdmin suspended tenant {Id} ({Name})", tenantId, tenant.TenantName);
                await SendAsync(chatId, $"🚫 مشتری <b>{tenant.TenantName}</b> معلق شد.", null, ct);
                await ShowTenantDetailAsync(chatId, tenantId, ct);
                break;

            case "activate":
                tenant.Status   = TenantStatus.Active;
                tenant.IsActive = true;
                _uow.Tenants.Update(tenant);
                await _uow.SaveChangesAsync(ct);
                _logger.LogInformation("SuperAdmin activated tenant {Id} ({Name})", tenantId, tenant.TenantName);
                await SendAsync(chatId, $"✅ مشتری <b>{tenant.TenantName}</b> فعال شد.", null, ct);
                await ShowTenantDetailAsync(chatId, tenantId, ct);
                break;

            case "plans":
                await ShowPlanPickerForTenantAsync(chatId, tenantId, ct);
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

        var expiry = tenant.ExpiresAt.HasValue
            ? tenant.ExpiresAt.Value.ToString("yyyy-MM-dd")
            : "♾ بدون انقضا";

        var html = $"🏢 <b>{tenant.TenantName}</b>\n" +
                   $"🔗 Slug: <code>{tenant.TenantSlug}</code>\n" +
                   $"📊 وضعیت: {GetStatusLabel(tenant.Status)}\n" +
                   (tenant.CustomerName  != null ? $"👤 مشتری: {tenant.CustomerName}\n"  : "") +
                   (tenant.CustomerPhone != null ? $"📞 تلفن: {tenant.CustomerPhone}\n"  : "") +
                   (tenant.BotUsername   != null ? $"🤖 بات: @{tenant.BotUsername}\n"    : "") +
                   $"⏰ انقضا: {expiry}\n" +
                   $"👥 حداکثر کاربر: {tenant.MaxUsers}\n" +
                   $"📦 حداکثر محصول: {tenant.MaxProducts}\n" +
                   $"🗓 ایجاد: {tenant.CreatedAt:yyyy-MM-dd}";

        var rows = new List<InlineKeyboardButton[]>();

        if (tenant.Status != TenantStatus.Suspended && tenant.Status != TenantStatus.Disabled)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🚫 تعلیق", $"sa:tenant:{tenantId}:suspend") });
        if (tenant.Status != TenantStatus.Active)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ فعال‌سازی", $"sa:tenant:{tenantId}:activate") });

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("📋 تغییر پلن", $"sa:tenant:{tenantId}:plans") });
        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ بازگشت", "sa:tenants:page:1") });

        await SendAsync(chatId, html, new InlineKeyboardMarkup(rows), ct);
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

        tenant.PlanId       = planId;
        tenant.MaxUsers     = plan.MaxUsers;
        tenant.MaxProducts  = plan.MaxProducts;
        tenant.MaxAdmins    = plan.MaxAdmins;
        _uow.Tenants.Update(tenant);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SuperAdmin set plan {Plan} for tenant {Id}", plan.Name, tenantId);
        await SendAsync(chatId, $"✅ پلن <b>{plan.Name}</b> برای <b>{tenant.TenantName}</b> تنظیم شد.", null, ct);
        await ShowTenantDetailAsync(chatId, tenantId, ct);
    }

    // ── Add-tenant wizard ─────────────────────────────────────────────────────

    private async Task StartAddTenantWizardAsync(long chatId, long telegramId, CancellationToken ct)
    {
        SaveConversation(telegramId, new SuperAdminConversation { State = SuperAdminState.AwaitingTenantName });
        await SendAsync(chatId,
            "➕ <b>افزودن مشتری جدید</b>\n\n📝 نام فروشگاه یا مشتری را وارد کنید:",
            BuildCancelKeyboard(), ct);
    }

    private async Task HandleWizardStepAsync(long chatId, long telegramId, string text, SuperAdminConversation conv, CancellationToken ct)
    {
        switch (conv.State)
        {
            case SuperAdminState.AwaitingTenantName:
                if (string.IsNullOrWhiteSpace(text))
                {
                    await SendAsync(chatId, "❌ نام نمی‌تواند خالی باشد. دوباره وارد کنید:", null, ct);
                    return;
                }
                conv.PendingTenantName = text.Trim();
                conv.State = SuperAdminState.AwaitingBotToken;
                SaveConversation(telegramId, conv);
                await SendAsync(chatId,
                    $"✅ نام: <b>{conv.PendingTenantName}</b>\n\n" +
                    "🤖 توکن بات تلگرام را وارد کنید:\n" +
                    "<i>(فرمت: 123456789:AAFxxxxxx)</i>",
                    BuildCancelKeyboard(), ct);
                break;

            case SuperAdminState.AwaitingBotToken:
                var token = text.Trim();
                if (!IsValidBotTokenFormat(token))
                {
                    await SendAsync(chatId,
                        "❌ فرمت توکن نامعتبر است.\n" +
                        "توکن باید به شکل <code>123456789:AAFxxxxxx</code> باشد.\n\n" +
                        "دوباره وارد کنید:", null, ct);
                    return;
                }
                conv.PendingBotToken = token;
                conv.State = SuperAdminState.ConfirmAddTenant;
                SaveConversation(telegramId, conv);

                var slug = GenerateSlug(conv.PendingTenantName!);
                await SendAsync(chatId,
                    "📋 <b>تأیید اطلاعات:</b>\n\n" +
                    $"🏢 نام: <b>{conv.PendingTenantName}</b>\n" +
                    $"🔗 Slug (پیش‌نویس): <code>{slug}</code>\n" +
                    $"🤖 توکن: <code>{MaskToken(token)}</code>\n\n" +
                    "آیا اطلاعات را تأیید می‌کنید؟",
                    new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("✅ تأیید",  $"sa:tenant:confirm:{telegramId}"),
                            InlineKeyboardButton.WithCallbackData("❌ لغو",    $"sa:tenant:cancel:{telegramId}")
                        }
                    }), ct);
                break;

            case SuperAdminState.AwaitingImpersonateTenantSlug:
                ClearConversation(telegramId);
                await HandleImpersonateInputAsync(chatId, text.Trim(), ct);
                break;
        }
    }

    private async Task FinalizeTenantCreationAsync(long chatId, long saId, CancellationToken ct)
    {
        var conv = GetConversation(saId);
        if (conv.State != SuperAdminState.ConfirmAddTenant ||
            string.IsNullOrEmpty(conv.PendingTenantName) ||
            string.IsNullOrEmpty(conv.PendingBotToken))
        {
            ClearConversation(saId);
            await SendAsync(chatId, "❌ اطلاعات نامعتبر یا منقضی شده است. لطفاً دوباره امتحان کنید.", BuildMainMenuKeyboard(), ct);
            return;
        }

        ClearConversation(saId);
        await CreateTenantAsync(chatId, conv.PendingTenantName, conv.PendingBotToken, ct);
    }

    private async Task CreateTenantAsync(long chatId, string tenantName, string rawToken, CancellationToken ct)
    {
        var slug           = await GenerateUniqueSlugAsync(tenantName);
        var encryptedToken = _aes.Encrypt(rawToken);
        var webhookSecret  = Guid.NewGuid().ToString("N")[..32];

        string? botUsername = null;
        var webhookSet = false;

        try
        {
            var newClient = new TelegramBotClient(rawToken);
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

        var tenant = new Tenant
        {
            TenantName        = tenantName,
            TenantSlug        = slug,
            BotTokenEncrypted = encryptedToken,
            WebhookSecret     = webhookSecret,
            BotUsername       = botUsername,
            Status            = webhookSet ? TenantStatus.Active : TenantStatus.PendingSetup,
            IsActive          = webhookSet,
            PlanId            = 1,
            MaxUsers          = 500,
            MaxProducts       = 50,
            MaxAdmins         = 2,
        };

        await _uow.Tenants.AddAsync(tenant);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SuperAdmin created tenant #{Id} ({Slug})", tenant.Id, slug);

        var statusNote = webhookSet
            ? $"✅ وب‌هوک ثبت شد:\n<code>{_opts.WebhookBaseUrl?.TrimEnd('/')}/api/telegram/{slug}/webhook</code>"
            : "⚠️ وب‌هوک به طور خودکار ثبت نشد.\n" +
              "مقدار <code>Telegram:WebhookBaseUrl</code> را در تنظیمات بررسی کنید.";

        var html = "🎉 <b>مشتری جدید با موفقیت ثبت شد!</b>\n\n" +
                   $"🏢 نام: <b>{tenantName}</b>\n" +
                   $"🔗 Slug: <code>{slug}</code>\n" +
                   $"🆔 شناسه: #{tenant.Id}\n" +
                   (botUsername != null ? $"🤖 بات: @{botUsername}\n" : "") +
                   $"\n{statusNote}";

        await SendAsync(chatId, html, BuildMainMenuKeyboard(), ct);
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
            await SendAsync(chatId,
                $"❌ مشتری با شناسه <code>{slug}</code> یافت نشد.",
                BuildMainMenuKeyboard(), ct);
            return;
        }

        var html = $"🏢 <b>{tenant.TenantName}</b>\n\n" +
                   $"🔗 Slug: <code>{tenant.TenantSlug}</code>\n" +
                   $"📊 وضعیت: {GetStatusLabel(tenant.Status)}\n" +
                   (tenant.BotUsername != null ? $"🤖 بات: @{tenant.BotUsername}\n" : "") +
                   "\n<i>برای ورود به پنل ادمین این مشتری، از طریق بات آن‌ها /start بزنید.\n" +
                   "قابلیت جعل هویت مستقیم در نسخه بعدی اضافه می‌شود.</i>";

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
            new[] { new KeyboardButton(BtnDashboard), new KeyboardButton(BtnTenants) },
            new[] { new KeyboardButton(BtnAddTenant),  new KeyboardButton(BtnPlans) },
            new[] { new KeyboardButton(BtnImpersonate) }
        })
        { ResizeKeyboard = true };

    private static ReplyKeyboardMarkup BuildCancelKeyboard() =>
        new(BtnCancel) { ResizeKeyboard = true };

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
