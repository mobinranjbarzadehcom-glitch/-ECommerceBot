using ECommerceBot.API.DTOs.Common;
using ECommerceBot.API.DTOs.User;
using ECommerceBot.API.DTOs.Wallet;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IUserService
{
    Task<ServiceResult<UserDto>> GetOrCreateUserAsync(long telegramId, string firstName, string? lastName, string? username);
    Task<ServiceResult<UserDto>> GetUserByTelegramIdAsync(long telegramId);
    Task<ServiceResult<decimal>> GetWalletBalanceAsync(int userId);
    Task<ServiceResult> ChargeWalletAsync(int userId, decimal amount, string? description = null);
    Task<ServiceResult> AddBonusAsync(int userId, decimal amount, string? description = null);
    Task<ServiceResult> AdjustWalletAsync(int userId, decimal amount, string description);
    Task<ServiceResult> RefundToWalletAsync(int userId, decimal amount, int? orderId, string? description = null);
    Task<ServiceResult<IEnumerable<WalletTransactionDto>>> GetWalletTransactionsAsync(int userId);
    Task<ServiceResult> BlockUserAsync(long telegramId);
    Task<ServiceResult> UnblockUserAsync(long telegramId);
    Task<ServiceResult<PagedResultDto<UserDto>>> GetAllUsersAsync(int page, int pageSize);
}
