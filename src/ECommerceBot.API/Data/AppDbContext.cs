using ECommerceBot.API.Data.Configurations;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Infrastructure.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // ── Platform tables (no tenant filter) ────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    // ── Tenant-scoped tables ───────────────────────────────────────────────────
    public DbSet<TelegramUser> TelegramUsers => Set<TelegramUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductKey> ProductKeys => Set<ProductKey>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<PaymentCard> PaymentCards => Set<PaymentCard>();
    public DbSet<BotSetting> BotSettings => Set<BotSetting>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LicenseInfo> LicenseInfos => Set<LicenseInfo>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<Affiliate> Affiliates => Set<Affiliate>();
    public DbSet<AffiliateReferral> AffiliateReferrals => Set<AffiliateReferral>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filters — only active when TenantContext is set.
        // Background services and migrations run without a set context and see all data.
        modelBuilder.Entity<TelegramUser>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Category>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Order>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<PaymentCard>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<WalletTransaction>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Ticket>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<BotSetting>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<LicenseInfo>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Coupon>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<CouponUsage>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<Affiliate>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<AffiliateReferral>().HasQueryFilter(
            e => !_tenantContext.IsSet || e.TenantId == _tenantContext.TenantId);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
        }
    }
}
