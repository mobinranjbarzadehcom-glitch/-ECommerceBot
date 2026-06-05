using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.User;
using ECommerceBot.API.DTOs.Wallet;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<UserDto>> GetOrCreateUserAsync(long telegramId, string firstName, string? lastName, string? username)
    {
        var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
        if (user is null)
        {
            user = new TelegramUser
            {
                TelegramId = telegramId,
                FirstName = firstName,
                LastName = lastName,
                Username = username
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();
        }
        return ServiceResult<UserDto>.Success(MapToDto(user));
    }

    public async Task<ServiceResult<UserDto>> GetUserByTelegramIdAsync(long telegramId)
    {
        var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
        return user is null
            ? ServiceResult<UserDto>.Failure("User not found")
            : ServiceResult<UserDto>.Success(MapToDto(user));
    }

    public async Task<ServiceResult<decimal>> GetWalletBalanceAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        return user is null
            ? ServiceResult<decimal>.Failure("User not found")
            : ServiceResult<decimal>.Success(user.WalletBalance);
    }

    public async Task<ServiceResult> ChargeWalletAsync(int userId, decimal amount, string? description = null)
    {
        if (amount <= 0)
            return ServiceResult.Failure("Charge amount must be positive");

        return await ExecuteWalletOperationAsync(userId, amount, WalletTransactionType.Charge, description);
    }

    public async Task<ServiceResult> AddBonusAsync(int userId, decimal amount, string? description = null)
    {
        if (amount <= 0)
            return ServiceResult.Failure("Bonus amount must be positive");

        return await ExecuteWalletOperationAsync(userId, amount, WalletTransactionType.Bonus, description);
    }

    public async Task<ServiceResult> AdjustWalletAsync(int userId, decimal amount, string description)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found");

        if (user.WalletBalance + amount < 0)
            return ServiceResult.Failure("Adjustment would result in negative balance");

        return await ExecuteWalletOperationAsync(userId, amount, WalletTransactionType.Adjustment, description, user);
    }

    public async Task<ServiceResult> RefundToWalletAsync(int userId, decimal amount, int? orderId, string? description = null)
    {
        if (amount <= 0)
            return ServiceResult.Failure("Refund amount must be positive");

        return await ExecuteWalletOperationAsync(userId, amount, WalletTransactionType.Refund,
            description ?? "Order refund", null, orderId);
    }

    public async Task<ServiceResult<IEnumerable<WalletTransactionDto>>> GetWalletTransactionsAsync(int userId)
    {
        var transactions = await _uow.WalletTransactions.GetByUserIdAsync(userId);
        return ServiceResult<IEnumerable<WalletTransactionDto>>.Success(transactions.Select(MapWalletTxToDto));
    }

    public async Task<ServiceResult> BlockUserAsync(long telegramId)
    {
        var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
        if (user is null)
            return ServiceResult.Failure("User not found");

        user.IsBlocked = true;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UnblockUserAsync(long telegramId)
    {
        var user = await _uow.Users.GetByTelegramIdAsync(telegramId);
        if (user is null)
            return ServiceResult.Failure("User not found");

        user.IsBlocked = false;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<PagedResultDto<UserDto>>> GetAllUsersAsync(int page, int pageSize)
    {
        var all = await _uow.Users.GetAllAsync();
        var list = all.ToList();
        var totalCount = list.Count;
        var items = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDto)
            .ToList();

        return ServiceResult<PagedResultDto<UserDto>>.Success(new PagedResultDto<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private async Task<ServiceResult> ExecuteWalletOperationAsync(
        int userId, decimal amount, WalletTransactionType type,
        string? description, TelegramUser? existingUser = null, int? orderId = null)
    {
        await _uow.BeginTransactionAsync();
        try
        {
            var user = existingUser ?? await _uow.Users.GetByIdAsync(userId);
            if (user is null)
            {
                await _uow.RollbackTransactionAsync();
                return ServiceResult.Failure("User not found");
            }

            var balanceBefore = user.WalletBalance;
            user.WalletBalance += amount;
            _uow.Users.Update(user);

            var walletTx = new WalletTransaction
            {
                UserId = userId,
                Amount = amount,
                BalanceBefore = balanceBefore,
                BalanceAfter = user.WalletBalance,
                Type = type,
                Description = description,
                RelatedOrderId = orderId
            };
            await _uow.WalletTransactions.AddAsync(walletTx);
            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();

            return ServiceResult.Success();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }
    }

    private static UserDto MapToDto(TelegramUser u) => new()
    {
        Id = u.Id,
        TelegramId = u.TelegramId,
        Username = u.Username,
        FirstName = u.FirstName,
        LastName = u.LastName,
        PhoneNumber = u.PhoneNumber,
        Role = u.Role,
        IsBlocked = u.IsBlocked,
        WalletBalance = u.WalletBalance,
        CreatedAt = u.CreatedAt
    };

    private static WalletTransactionDto MapWalletTxToDto(WalletTransaction wt) => new()
    {
        Id = wt.Id,
        UserId = wt.UserId,
        Amount = wt.Amount,
        BalanceBefore = wt.BalanceBefore,
        BalanceAfter = wt.BalanceAfter,
        Type = wt.Type,
        Description = wt.Description,
        RelatedOrderId = wt.RelatedOrderId,
        CreatedAt = wt.CreatedAt
    };
}
