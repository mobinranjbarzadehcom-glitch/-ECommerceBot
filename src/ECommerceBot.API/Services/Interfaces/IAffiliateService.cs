using ECommerceBot.API.Entities;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IAffiliateService
{
    Task<ServiceResult<Affiliate>> GetOrCreateAffiliateAsync(int userId);
    Task<ServiceResult> TrackReferralAsync(string referralCode, int newUserId);
    Task<ServiceResult<Affiliate?>> GetAffiliateByUserIdAsync(int userId);
}
