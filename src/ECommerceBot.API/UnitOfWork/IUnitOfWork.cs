using ECommerceBot.API.Repositories.Interfaces;

namespace ECommerceBot.API.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
