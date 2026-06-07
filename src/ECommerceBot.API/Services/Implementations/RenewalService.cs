using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class RenewalService : IRenewalService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<RenewalService> _logger;

    public RenewalService(IUnitOfWork uow, ILogger<RenewalService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<ServiceResult<RenewalRequest>> CreateRenewalRequestAsync(
        int tenantId, long requesterTelegramId, int durationMonths, string? receiptFileId)
    {
        if (durationMonths is not (1 or 3 or 6 or 12))
            return ServiceResult<RenewalRequest>.Failure("مدت اشتراک نامعتبر است.");

        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null)
            return ServiceResult<RenewalRequest>.Failure("فروشگاه یافت نشد.");

        var request = new RenewalRequest
        {
            TenantId = tenantId,
            RequestType = RenewalRequestType.Renewal,
            DurationMonths = durationMonths,
            ReceiptFileId = receiptFileId,
            RequesterTelegramId = requesterTelegramId,
            Status = RenewalRequestStatus.Pending
        };

        await _uow.RenewalRequests.AddAsync(request);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Renewal request created for tenant {TenantId} — {Months} months", tenantId, durationMonths);
        return ServiceResult<RenewalRequest>.Success(request);
    }

    public async Task<ServiceResult<RenewalRequest>> CreateUpgradeRequestAsync(
        int tenantId, long requesterTelegramId, int newPlanId)
    {
        var tenant = await _uow.Tenants.GetByIdAsync(tenantId);
        if (tenant is null)
            return ServiceResult<RenewalRequest>.Failure("فروشگاه یافت نشد.");

        var plan = await _uow.SubscriptionPlans.GetByIdAsync(newPlanId);
        if (plan is null)
            return ServiceResult<RenewalRequest>.Failure("پلن یافت نشد.");

        if (tenant.PlanId == newPlanId)
            return ServiceResult<RenewalRequest>.Failure("شما در حال حاضر این پلن را دارید.");

        var request = new RenewalRequest
        {
            TenantId = tenantId,
            RequestType = RenewalRequestType.Upgrade,
            DurationMonths = 0,
            NewPlanId = newPlanId,
            RequesterTelegramId = requesterTelegramId,
            Status = RenewalRequestStatus.Pending
        };

        await _uow.RenewalRequests.AddAsync(request);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Upgrade request created for tenant {TenantId} → plan {PlanId}", tenantId, newPlanId);
        return ServiceResult<RenewalRequest>.Success(request);
    }

    public async Task<ServiceResult> ApproveAsync(int requestId, string? note = null)
    {
        var request = await _uow.RenewalRequests.GetByIdAsync(requestId);
        if (request is null)
            return ServiceResult.Failure("درخواست یافت نشد.");

        if (request.Status != RenewalRequestStatus.Pending)
            return ServiceResult.Failure("این درخواست قبلاً بررسی شده است.");

        var tenant = await _uow.Tenants.GetByIdAsync(request.TenantId);
        if (tenant is null)
            return ServiceResult.Failure("فروشگاه یافت نشد.");

        request.Status = RenewalRequestStatus.Approved;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = note;

        if (request.RequestType == RenewalRequestType.Renewal && request.DurationMonths > 0)
        {
            var baseDate = tenant.ExpiresAt.HasValue && tenant.ExpiresAt > DateTime.UtcNow
                ? tenant.ExpiresAt.Value
                : DateTime.UtcNow;

            tenant.ExpiresAt = baseDate.AddMonths(request.DurationMonths);
            tenant.TrialEndsAt = null;
            tenant.IsTrial = false;
            tenant.Status = TenantStatus.Active;
            tenant.IsActive = true;
            _uow.Tenants.Update(tenant);
        }
        else if (request.RequestType == RenewalRequestType.Upgrade && request.NewPlanId.HasValue)
        {
            var plan = await _uow.SubscriptionPlans.GetByIdAsync(request.NewPlanId.Value);
            if (plan is not null)
            {
                tenant.PlanId = plan.Id;
                tenant.MaxUsers = plan.MaxUsers;
                tenant.MaxProducts = plan.MaxProducts;
                tenant.MaxAdmins = plan.MaxAdmins;
                tenant.MaxOrdersPerMonth = plan.MaxOrdersPerMonth;
                _uow.Tenants.Update(tenant);
            }
        }

        _uow.RenewalRequests.Update(request);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Renewal request {Id} approved for tenant {TenantId}", requestId, request.TenantId);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> RejectAsync(int requestId, string? note = null)
    {
        var request = await _uow.RenewalRequests.GetByIdAsync(requestId);
        if (request is null)
            return ServiceResult.Failure("درخواست یافت نشد.");

        if (request.Status != RenewalRequestStatus.Pending)
            return ServiceResult.Failure("این درخواست قبلاً بررسی شده است.");

        request.Status = RenewalRequestStatus.Rejected;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNote = note;
        _uow.RenewalRequests.Update(request);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Renewal request {Id} rejected for tenant {TenantId}", requestId, request.TenantId);
        return ServiceResult.Success();
    }

    public async Task<IEnumerable<RenewalRequest>> GetPendingRequestsAsync() =>
        await _uow.RenewalRequests.GetPendingAsync();

    public async Task<IEnumerable<RenewalRequest>> GetByTenantIdAsync(int tenantId) =>
        await _uow.RenewalRequests.GetByTenantIdAsync(tenantId);
}
