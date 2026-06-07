using ECommerceBot.API.Data;
using ECommerceBot.API.Infrastructure.Audit;
using ECommerceBot.API.Infrastructure.Background;
using ECommerceBot.API.Infrastructure.Backup;
using ECommerceBot.API.Infrastructure.Cache;
using ECommerceBot.API.Infrastructure.HealthChecks;
using ECommerceBot.API.Infrastructure.Licensing;
using ECommerceBot.API.Infrastructure.Localization;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Infrastructure.RateLimit;
using ECommerceBot.API.Infrastructure.Security;
using ECommerceBot.API.Infrastructure.Startup;
using ECommerceBot.API.Middleware;
using ECommerceBot.API.Repositories.Implementations;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Services.Implementations;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.Telegram;
using ECommerceBot.API.Telegram.Handlers;
using ECommerceBot.API.Telegram.Keyboards;
using ECommerceBot.API.Telegram.Messages;
using ECommerceBot.API.Telegram.Options;
using ECommerceBot.API.Telegram.Services;
using ECommerceBot.API.Telegram.States;
using ECommerceBot.API.UnitOfWork;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.RateLimiting;
using Telegram.Bot;

// ── Bootstrap Serilog before anything can fail ────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, svc, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(svc)
           .Enrich.FromLogContext()
           .WriteTo.Logger(sub => sub
               .Filter.ByIncludingOnly(e =>
                   e.Properties.TryGetValue("SourceContext", out var sc) &&
                   sc.ToString().Contains("ECommerceBot.API.Telegram"))
               .WriteTo.File(
                   "Logs/telegram-.log",
                   rollingInterval: RollingInterval.Day,
                   retainedFileCountLimit: 30,
                   outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));
    });

    // ── Options ───────────────────────────────────────────────────────────────
    builder.Services.Configure<TelegramOptions>(
        builder.Configuration.GetSection(TelegramOptions.SectionName));
    builder.Services.Configure<BackupOptions>(
        builder.Configuration.GetSection(BackupOptions.SectionName));
    builder.Services.Configure<LicenseOptions>(
        builder.Configuration.GetSection(LicenseOptions.SectionName));

    // ── DbContext ─────────────────────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // ── Caching: Redis (with in-memory fallback) ──────────────────────────────
    builder.Services.AddMemoryCache(opts => opts.SizeLimit = 1024);

    var redisConnStr = builder.Configuration["Redis:ConnectionString"];
    var redisInstanceName = builder.Configuration["Redis:InstanceName"] ?? "ECommerceBot:";
    var redisEnabled = !string.IsNullOrWhiteSpace(redisConnStr);

    if (redisEnabled)
    {
        try
        {
            var multiplexer = ConnectionMultiplexer.Connect(redisConnStr!);
            builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
            builder.Services.AddStackExchangeRedisCache(opts =>
            {
                opts.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer);
                opts.InstanceName = redisInstanceName;
            });
            Log.Information("Redis cache registered: {ConnectionString}", redisConnStr);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Redis connection failed — falling back to in-memory distributed cache");
            builder.Services.AddDistributedMemoryCache();
            redisEnabled = false;
        }
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
    }

    builder.Services.AddScoped<ICacheService, CacheService>();

    // ── Multi-tenancy ─────────────────────────────────────────────────────────
    builder.Services.AddScoped<ITenantContext, TenantContext>();
    builder.Services.AddScoped<ITenantResolver, TenantResolver>();
    builder.Services.AddSingleton<ITenantBotClientFactory, TenantBotClientFactory>();
    builder.Services.AddSingleton<IAesEncryptionService, AesEncryptionService>();

    // ── Repositories ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<ITenantRepository, TenantRepository>();
    builder.Services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
    builder.Services.AddScoped<IProductRepository, ProductRepository>();
    builder.Services.AddScoped<IProductKeyRepository, ProductKeyRepository>();
    builder.Services.AddScoped<ICartRepository, CartRepository>();
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
    builder.Services.AddScoped<ITicketRepository, TicketRepository>();
    builder.Services.AddScoped<ITicketMessageRepository, TicketMessageRepository>();
    builder.Services.AddScoped<IBotSettingRepository, BotSettingRepository>();
    builder.Services.AddScoped<IPaymentCardRepository, PaymentCardRepository>();
    builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
    builder.Services.AddScoped<ILicenseRepository, LicenseRepository>();
    builder.Services.AddScoped<ICouponRepository, CouponRepository>();
    builder.Services.AddScoped<ICouponUsageRepository, CouponUsageRepository>();
    builder.Services.AddScoped<IAffiliateRepository, AffiliateRepository>();
    builder.Services.AddScoped<IAffiliateReferralRepository, AffiliateReferralRepository>();
    // Phase 6 repositories
    builder.Services.AddScoped<ITenantNoteRepository, TenantNoteRepository>();
    builder.Services.AddScoped<IRenewalRequestRepository, RenewalRequestRepository>();
    builder.Services.AddScoped<IScheduledBroadcastRepository, ScheduledBroadcastRepository>();
    builder.Services.AddScoped<IFaqItemRepository, FaqItemRepository>();

    // ── UnitOfWork ────────────────────────────────────────────────────────────
    builder.Services.AddScoped<IUnitOfWork, ECommerceBot.API.UnitOfWork.UnitOfWork>();

    // ── Business Services ─────────────────────────────────────────────────────
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<ICouponService, CouponService>();
    builder.Services.AddScoped<IAffiliateService, AffiliateService>();
    builder.Services.AddScoped<IBroadcastService, BroadcastService>();
    builder.Services.AddScoped<IExportService, ExportService>();
    builder.Services.AddScoped<IAiSupportService, AiSupportService>();
    builder.Services.AddScoped<ISettingService, SettingService>();
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<IOrderService, OrderService>();
    builder.Services.AddScoped<ITicketService, TicketService>();
    builder.Services.AddScoped<IAdminService, AdminService>();
    // Phase 6 services
    builder.Services.AddScoped<IRenewalService, RenewalService>();
    builder.Services.AddScoped<IResourceUsageService, ResourceUsageService>();
    builder.Services.AddScoped<IFaqService, FaqService>();
    builder.Services.AddScoped<IBackupManagementService, BackupManagementService>();

    // ── Infrastructure Services ────────────────────────────────────────────────
    builder.Services.AddScoped<IAuditLogService, AuditLogService>();
    builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

    // ── Licensing ─────────────────────────────────────────────────────────────
    builder.Services.AddSingleton<LicenseStatusCache>();
    builder.Services.AddSingleton<IServerFingerprintService, ServerFingerprintService>();
    builder.Services.AddSingleton<ILicenseSignatureValidator, RsaLicenseSignatureValidator>();
    builder.Services.AddScoped<ILicenseService, LicenseService>();

    // ── Localization ──────────────────────────────────────────────────────────
    builder.Services.AddScoped<ILocalizationService, LocalizationService>();

    // ── Background Services ───────────────────────────────────────────────────
    builder.Services.AddHostedService<OrderExpirationService>();
    builder.Services.AddHostedService<DatabaseBackupService>();
    builder.Services.AddHostedService<LicenseValidationBackgroundService>();
    builder.Services.AddHostedService<TenantExpiryNotificationService>();
    // Phase 6 background services
    builder.Services.AddSingleton<BotHealthBackgroundService>();
    builder.Services.AddSingleton<IBotHealthService>(sp => sp.GetRequiredService<BotHealthBackgroundService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BotHealthBackgroundService>());
    builder.Services.AddHostedService<BroadcastSchedulerService>();

    // ── Telegram Bot Client (singleton) ──────────────────────────────────────
    builder.Services.AddSingleton<ITelegramBotClient>(provider =>
    {
        var token = builder.Configuration["Telegram:BotToken"] ?? string.Empty;
        return new TelegramBotClient(token);
    });

    // ── Telegram Services ─────────────────────────────────────────────────────
    builder.Services.AddScoped<IBotTextService, BotTextService>();
    builder.Services.AddScoped<IConversationManager, ConversationManager>();
    builder.Services.AddScoped<ITelegramMessageService, TelegramMessageService>();
    builder.Services.AddScoped<IKeyboardBuilder, KeyboardBuilder>();
    builder.Services.AddScoped<IMessageHandler, MessageHandler>();
    builder.Services.AddScoped<ICallbackQueryHandler, CallbackQueryHandler>();
    builder.Services.AddScoped<ISuperAdminHandler, SuperAdminHandler>();
    builder.Services.AddScoped<IUpdateDispatcher, UpdateDispatcher>();

    // ── Rate Limiting ─────────────────────────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("webhook", opt =>
        {
            opt.PermitLimit = 300;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ── Health Checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "db", "live", "ready" })
        .AddCheck<RedisHealthCheck>("redis",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "redis", "ready" })
        .AddCheck<BackupDirectoryHealthCheck>("backup-directory",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "backup", "ready" });

    // ── Web ───────────────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.ConfigureTelegramBotMvc();
    builder.Services.AddEndpointsApiExplorer();

    var app = builder.Build();

    // ── Startup validation ────────────────────────────────────────────────────
    StartupValidator.Validate(app.Configuration, app.Environment, app.Logger);

    // ── Auto-migrate + seed default tenant ───────────────────────────────────
    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied");

        // Ensure default tenant's bot token is stored encrypted
        var defaultTenant = await db.Tenants.FindAsync(1);
        if (defaultTenant is not null && string.IsNullOrEmpty(defaultTenant.BotTokenEncrypted))
        {
            var aes = scope.ServiceProvider.GetRequiredService<IAesEncryptionService>();
            var rawToken = app.Configuration["Telegram:BotToken"] ?? string.Empty;
            if (!string.IsNullOrEmpty(rawToken))
            {
                defaultTenant.BotTokenEncrypted = aes.Encrypt(rawToken);
                await db.SaveChangesAsync();
                Log.Information("Default tenant bot token encrypted and stored");
            }
        }
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });
    app.UseMiddleware<LicenseMiddleware>();
    app.UseRateLimiter();
    if (app.Environment.IsDevelopment())
        app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    // ── Health check endpoints ────────────────────────────────────────────────
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = WriteHealthResponse
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("live"),
        ResponseWriter = WriteHealthResponse
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponse
    });

    Log.Information("ECommerceBot API starting up (Redis: {RedisEnabled})", redisEnabled);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

// ── Health check JSON response writer ────────────────────────────────────────
static async Task WriteHealthResponse(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json; charset=utf-8";
    ctx.Response.StatusCode = report.Status == HealthStatus.Healthy ? 200 : 503;

    var result = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            durationMs = e.Value.Duration.TotalMilliseconds,
            error = e.Value.Exception?.Message
        })
    };

    await ctx.Response.WriteAsync(
        JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
}
