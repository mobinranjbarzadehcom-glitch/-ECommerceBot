using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Infrastructure.Licensing;

public interface ILicenseSignatureValidator
{
    bool Validate(LicenseInfo license);
}
