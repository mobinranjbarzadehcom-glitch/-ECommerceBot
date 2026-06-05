using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ILicenseRepository : IGenericRepository<LicenseInfo>
{
    Task<LicenseInfo?> GetActiveAsync();
    Task<LicenseInfo?> GetByKeyAsync(string licenseKey);
}
