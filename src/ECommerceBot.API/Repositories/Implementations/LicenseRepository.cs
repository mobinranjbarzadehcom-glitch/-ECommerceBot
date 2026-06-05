using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class LicenseRepository : GenericRepository<LicenseInfo>, ILicenseRepository
{
    public LicenseRepository(AppDbContext context) : base(context) { }

    public async Task<LicenseInfo?> GetActiveAsync() =>
        await _dbSet.Where(l => l.IsActive).OrderByDescending(l => l.IssuedAt).FirstOrDefaultAsync();

    public async Task<LicenseInfo?> GetByKeyAsync(string licenseKey) =>
        await _dbSet.SingleOrDefaultAsync(l => l.LicenseKey == licenseKey);
}
