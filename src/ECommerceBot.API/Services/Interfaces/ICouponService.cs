using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public record CouponValidationResult(Coupon Coupon, decimal DiscountAmount);

public interface ICouponService
{
    Task<ServiceResult<CouponValidationResult>> ValidateAsync(string code, int userId, decimal orderAmount);
    Task<ServiceResult<Coupon>> CreateAsync(string code, DiscountType discountType, decimal discountValue, decimal? minOrderAmount, int? maxUses, DateTime? expiresAt);
    Task<ServiceResult<IEnumerable<Coupon>>> GetAllAsync();
    Task<ServiceResult> ToggleActiveAsync(int couponId);
    Task RecordUsageAsync(int couponId, int userId, int? orderId);
}
