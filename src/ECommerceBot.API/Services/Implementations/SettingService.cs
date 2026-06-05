using ECommerceBot.API.DTOs.Payment;
using ECommerceBot.API.DTOs.Setting;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class SettingService : ISettingService
{
    private readonly IUnitOfWork _uow;

    public SettingService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ServiceResult<string>> GetSettingAsync(string key)
    {
        var value = await _uow.BotSettings.GetValueAsync(key);
        return value is null
            ? ServiceResult<string>.Failure($"Setting '{key}' not found")
            : ServiceResult<string>.Success(value);
    }

    public async Task<ServiceResult> SetSettingAsync(string key, string value, string? description = null)
    {
        await _uow.BotSettings.UpsertAsync(key, value, description);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false)
    {
        var value = await _uow.BotSettings.GetValueAsync(key);
        if (value is null) return defaultValue;
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<string?> GetStringSettingAsync(string key) =>
        await _uow.BotSettings.GetValueAsync(key);

    public async Task<bool> IsCardRotationEnabledAsync() =>
        await GetBoolSettingAsync(BotSettingKeys.IsCardRotationEnabled, false);

    public async Task<ServiceResult<PaymentCardDto>> GetActivePaymentCardAsync()
    {
        var rotationEnabled = await IsCardRotationEnabledAsync();

        if (!rotationEnabled)
        {
            var defaultCard = await _uow.PaymentCards.GetDefaultCardAsync();
            if (defaultCard is null)
            {
                var firstActive = (await _uow.PaymentCards.GetActiveCardsAsync()).FirstOrDefault();
                if (firstActive is null)
                    return ServiceResult<PaymentCardDto>.Failure("No active payment card found");
                return ServiceResult<PaymentCardDto>.Success(MapCardToDto(firstActive));
            }
            return ServiceResult<PaymentCardDto>.Success(MapCardToDto(defaultCard));
        }

        // Card rotation: pick next after last used
        var lastUsedIdStr = await _uow.BotSettings.GetValueAsync(BotSettingKeys.LastUsedCardId);
        int lastUsedId = int.TryParse(lastUsedIdStr, out var id) ? id : 0;

        var nextCard = await _uow.PaymentCards.GetNextRotationCardAsync(lastUsedId);
        if (nextCard is null)
            return ServiceResult<PaymentCardDto>.Failure("No active payment card found");

        await _uow.BotSettings.UpsertAsync(BotSettingKeys.LastUsedCardId, nextCard.Id.ToString());
        await _uow.SaveChangesAsync();

        return ServiceResult<PaymentCardDto>.Success(MapCardToDto(nextCard));
    }

    public async Task<ServiceResult<IEnumerable<BotSettingDto>>> GetAllSettingsAsync()
    {
        var settings = await _uow.BotSettings.GetAllAsync();
        return ServiceResult<IEnumerable<BotSettingDto>>.Success(settings.Select(s => new BotSettingDto
        {
            Id = s.Id,
            Key = s.Key,
            Value = s.Value,
            Description = s.Description
        }));
    }

    private static PaymentCardDto MapCardToDto(Entities.PaymentCard card) => new()
    {
        Id = card.Id,
        CardNumber = card.CardNumber,
        CardHolderName = card.CardHolderName,
        BankName = card.BankName,
        IsDefault = card.IsDefault,
        DisplayOrder = card.DisplayOrder
    };
}
