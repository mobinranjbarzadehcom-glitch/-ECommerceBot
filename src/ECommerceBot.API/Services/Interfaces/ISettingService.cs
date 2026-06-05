using ECommerceBot.API.DTOs.Payment;
using ECommerceBot.API.DTOs.Setting;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface ISettingService
{
    Task<ServiceResult<string>> GetSettingAsync(string key);
    Task<ServiceResult> SetSettingAsync(string key, string value, string? description = null);
    Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false);
    Task<string?> GetStringSettingAsync(string key);
    Task<bool> IsCardRotationEnabledAsync();
    Task<ServiceResult<PaymentCardDto>> GetActivePaymentCardAsync();
    Task<ServiceResult<IEnumerable<BotSettingDto>>> GetAllSettingsAsync();
}
