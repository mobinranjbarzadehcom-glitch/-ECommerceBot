using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.Infrastructure.Multitenancy;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class AffiliateService : IAffiliateService
{
    private const decimal ReferralBonusAmount = 5m;

    private readonly IUnitOfWork _uow;
    private readonly IUserService _userService;

    public AffiliateService(IUnitOfWork uow, IUserService userService)
    {
        _uow = uow;
        _userService = userService;
    }

    public async Task<ServiceResult<Affiliate>> GetOrCreateAffiliateAsync(int userId)
    {
        var existing = await _uow.Affiliates.GetByUserIdAsync(userId);
        if (existing is not null)
            return ServiceResult<Affiliate>.Success(existing);

        var code = GenerateCode(userId);
        var affiliate = new Affiliate
        {
            UserId = userId,
            ReferralCode = code,
            IsActive = true
        };
        await _uow.Affiliates.AddAsync(affiliate);
        await _uow.SaveChangesAsync();
        return ServiceResult<Affiliate>.Success(affiliate);
    }

    public async Task<ServiceResult> TrackReferralAsync(string referralCode, int newUserId)
    {
        if (await _uow.Affiliates.IsUserReferredAsync(newUserId))
            return ServiceResult.Failure("کاربر قبلاً از طریق معرف ثبت‌نام کرده است.");

        var affiliate = await _uow.Affiliates.GetByCodeAsync(referralCode);
        if (affiliate is null)
            return ServiceResult.Failure("کد معرف نامعتبر است.");

        // Prevent self-referral
        if (affiliate.UserId == newUserId)
            return ServiceResult.Failure("نمی‌توانید خودتان را معرفی کنید.");

        var referral = new AffiliateReferral
        {
            AffiliateId = affiliate.Id,
            ReferredUserId = newUserId,
            BonusAmount = ReferralBonusAmount
        };
        await _uow.AffiliateReferrals.AddAsync(referral);

        affiliate.TotalReferrals++;
        affiliate.TotalEarnings += ReferralBonusAmount;
        _uow.Affiliates.Update(affiliate);

        await _uow.SaveChangesAsync();

        // Credit bonus to referrer
        await _userService.AddBonusAsync(affiliate.UserId, ReferralBonusAmount,
            $"پاداش معرفی کاربر جدید");

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<Affiliate?>> GetAffiliateByUserIdAsync(int userId)
    {
        var affiliate = await _uow.Affiliates.GetByUserIdAsync(userId);
        return ServiceResult<Affiliate?>.Success(affiliate);
    }

    private static string GenerateCode(int userId)
    {
        var random = Guid.NewGuid().ToString("N")[..6].ToUpper();
        return $"REF{userId}{random}";
    }
}
