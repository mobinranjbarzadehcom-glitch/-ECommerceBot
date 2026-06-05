using ECommerceBot.API.Data;
using ECommerceBot.API.Repositories.Implementations;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ECommerceBot.API.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    private IUserRepository? _users;
    private ICategoryRepository? _categories;
    private IProductRepository? _products;
    private IProductKeyRepository? _productKeys;
    private ICartRepository? _carts;
    private IOrderRepository? _orders;
    private ITransactionRepository? _transactions;
    private IWalletTransactionRepository? _walletTransactions;
    private ITicketRepository? _tickets;
    private ITicketMessageRepository? _ticketMessages;
    private IBotSettingRepository? _botSettings;
    private IPaymentCardRepository? _paymentCards;
    private IAuditLogRepository? _auditLogs;
    private ILicenseRepository? _licenses;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users =>
        _users ??= new UserRepository(_context);

    public ICategoryRepository Categories =>
        _categories ??= new CategoryRepository(_context);

    public IProductRepository Products =>
        _products ??= new ProductRepository(_context);

    public IProductKeyRepository ProductKeys =>
        _productKeys ??= new ProductKeyRepository(_context);

    public ICartRepository Carts =>
        _carts ??= new CartRepository(_context);

    public IOrderRepository Orders =>
        _orders ??= new OrderRepository(_context);

    public ITransactionRepository Transactions =>
        _transactions ??= new TransactionRepository(_context);

    public IWalletTransactionRepository WalletTransactions =>
        _walletTransactions ??= new WalletTransactionRepository(_context);

    public ITicketRepository Tickets =>
        _tickets ??= new TicketRepository(_context);

    public ITicketMessageRepository TicketMessages =>
        _ticketMessages ??= new TicketMessageRepository(_context);

    public IBotSettingRepository BotSettings =>
        _botSettings ??= new BotSettingRepository(_context);

    public IPaymentCardRepository PaymentCards =>
        _paymentCards ??= new PaymentCardRepository(_context);

    public IAuditLogRepository AuditLogs =>
        _auditLogs ??= new AuditLogRepository(_context);

    public ILicenseRepository Licenses =>
        _licenses ??= new LicenseRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync() =>
        _currentTransaction = await _context.Database.BeginTransactionAsync();

    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.CommitAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.RollbackAsync();
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
