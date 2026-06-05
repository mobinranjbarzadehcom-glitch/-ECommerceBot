using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class CouponService : ICouponService
{
    private readonly IUnitOfWork _uow;
    private readonly ITenantContext _tenantContext;

    public CouponService(IUnitOfWork uow, ITenantContext tenantContext)
    {
        _uow = uow;
        _tenantContext = tenantContext;
    }

    public async Task<ServiceResult<CouponValidationResult>> ValidateAsync(string code, int userId, decimal orderAmount)
    {
        var coupon = await _uow.Coupons.GetByCodeAsync(code);
        if (coupon is null || !coupon.IsActive)
            return ServiceResult<CouponValidationResult>.Failure("کد تخفیف نامعتبر است.");

        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt < DateTime.UtcNow)
            return ServiceResult<CouponValidationResult>.Failure("کد تخفیف منقضی شده است.");

        if (coupon.MaxUses.HasValue && coupon.UsedCount >= coupon.MaxUses.Value)
            return ServiceResult<CouponValidationResult>.Failure("ظرفیت این کد تخفیف تمام شده است.");

        if (coupon.MinOrderAmount.HasValue && orderAmount < coupon.MinOrderAmount.Value)
            return ServiceResult<CouponValidationResult>.Failure(
                $"حداقل مبلغ سفارش برای استفاده از این کد {coupon.MinOrderAmount.Value:F0} تومان است.");

        if (await _uow.Coupons.HasUserUsedCouponAsync(coupon.Id, userId))
            return ServiceResult<CouponValidationResult>.Failure("قبلاً از این کد تخفیف استفاده کرده‌اید.");

        var discount = coupon.DiscountType == DiscountType.Percentage
            ? Math.Round(orderAmount * coupon.DiscountValue / 100, 2)
            : Math.Min(coupon.DiscountValue, orderAmount);

        return ServiceResult<CouponValidationResult>.Success(new CouponValidationResult(coupon, discount));
    }

    public async Task<ServiceResult<Coupon>> CreateAsync(
        string code, DiscountType discountType, decimal discountValue,
        decimal? minOrderAmount, int? maxUses, DateTime? expiresAt)
    {
        var normalized = code.Trim().ToUpper();
        if (normalized.Length < 3 || normalized.Length > 50)
            return ServiceResult<Coupon>.Failure("کد باید بین ۳ تا ۵۰ کاراکتر باشد.");

        var existing = await _uow.Coupons.GetByCodeAsync(normalized);
        if (existing is not null)
            return ServiceResult<Coupon>.Failure("این کد قبلاً ثبت شده است.");

        if (discountType == DiscountType.Percentage && (discountValue <= 0 || discountValue > 100))
            return ServiceResult<Coupon>.Failure("درصد تخفیف باید بین ۱ تا ۱۰۰ باشد.");

        if (discountType == DiscountType.Fixed && discountValue <= 0)
            return ServiceResult<Coupon>.Failure("مبلغ تخفیف باید بیشتر از صفر باشد.");

        var coupon = new Coupon
        {
            Code = normalized,
            DiscountType = discountType,
            DiscountValue = discountValue,
            MinOrderAmount = minOrderAmount,
            MaxUses = maxUses,
            ExpiresAt = expiresAt,
            IsActive = true
        };
        await _uow.Coupons.AddAsync(coupon);
        await _uow.SaveChangesAsync();
        return ServiceResult<Coupon>.Success(coupon);
    }

    public async Task<ServiceResult<IEnumerable<Coupon>>> GetAllAsync()
    {
        var coupons = await _uow.Coupons.GetAllAsync();
        return ServiceResult<IEnumerable<Coupon>>.Success(coupons);
    }

    public async Task<ServiceResult> ToggleActiveAsync(int couponId)
    {
        var coupon = await _uow.Coupons.GetByIdAsync(couponId);
        if (coupon is null)
            return ServiceResult.Failure("کوپن پیدا نشد.");

        coupon.IsActive = !coupon.IsActive;
        _uow.Coupons.Update(coupon);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task RecordUsageAsync(int couponId, int userId, int? orderId)
    {
        var coupon = await _uow.Coupons.GetByIdAsync(couponId);
        if (coupon is null) return;

        coupon.UsedCount++;
        _uow.Coupons.Update(coupon);

        await _uow.CouponUsages.AddAsync(new CouponUsage
        {
            CouponId = couponId,
            UserId = userId,
            OrderId = orderId,
            UsedAt = DateTime.UtcNow
        });
        await _uow.SaveChangesAsync();
    }
}
