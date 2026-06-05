using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Infrastructure.Licensing;

/// <summary>
/// Singleton holder for the most-recently validated license result.
/// Updated by LicenseValidationBackgroundService; read by LicenseMiddleware.
/// </summary>
public sealed class LicenseStatusCache
{
    private LicenseValidationResult _result = LicenseValidationResult.NotActivated();

    public LicenseValidationResult Result
    {
        get => _result;
        set => _result = value;
    }

    public DateTime? LastCheckedAt { get; set; }
}
