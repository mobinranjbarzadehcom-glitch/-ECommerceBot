using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IRenewalService
{
    Task<ServiceResult<RenewalRequest>> CreateRenewalRequestAsync(
        int tenantId, long requesterTelegramId, int durationMonths, string? receiptFileId);

    Task<ServiceResult<RenewalRequest>> CreateUpgradeRequestAsync(
        int tenantId, long requesterTelegramId, int newPlanId);

    Task<ServiceResult> ApproveAsync(int requestId, string? note = null);
    Task<ServiceResult> RejectAsync(int requestId, string? note = null);

    Task<IEnumerable<RenewalRequest>> GetPendingRequestsAsync();
    Task<IEnumerable<RenewalRequest>> GetByTenantIdAsync(int tenantId);
}
