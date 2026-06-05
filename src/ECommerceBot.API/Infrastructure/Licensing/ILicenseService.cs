using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Infrastructure.Licensing;

public interface ILicenseService
{
    Task<LicenseValidationResult> ValidateAsync(CancellationToken ct = default);
    Task<LicenseValidationResult> ActivateAsync(string licensePackage, CancellationToken ct = default);
    LicenseValidationResult GetCachedResult();
}
