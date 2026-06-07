using ECommerceBot.API.Repositories.Interfaces;

namespace ECommerceBot.API.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    ITenantRepository Tenants { get; }
    ISubscriptionPlanRepository SubscriptionPlans { get; }
    IUserRepository Users { get; }
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    IProductKeyRepository ProductKeys { get; }
    ICartRepository Carts { get; }
    IOrderRepository Orders { get; }
    ITransactionRepository Transactions { get; }
    IWalletTransactionRepository WalletTransactions { get; }
    ITicketRepository Tickets { get; }
    ITicketMessageRepository TicketMessages { get; }
    IBotSettingRepository BotSettings { get; }
    IPaymentCardRepository PaymentCards { get; }
    IAuditLogRepository AuditLogs { get; }
    ILicenseRepository Licenses { get; }
    ICouponRepository Coupons { get; }
    ICouponUsageRepository CouponUsages { get; }
    IAffiliateRepository Affiliates { get; }
    IAffiliateReferralRepository AffiliateReferrals { get; }

    // Phase 6
    ITenantNoteRepository TenantNotes { get; }
    IRenewalRequestRepository RenewalRequests { get; }
    IScheduledBroadcastRepository ScheduledBroadcasts { get; }
    IFaqItemRepository FaqItems { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
