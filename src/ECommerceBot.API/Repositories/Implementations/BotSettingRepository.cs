using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class BotSettingRepository : GenericRepository<BotSetting>, IBotSettingRepository
{
    public BotSettingRepository(AppDbContext context) : base(context) { }

    public async Task<BotSetting?> GetByKeyAsync(string key) =>
        await _dbSet.SingleOrDefaultAsync(bs => bs.Key == key);

    public async Task<string?> GetValueAsync(string key)
    {
        var setting = await _dbSet.SingleOrDefaultAsync(bs => bs.Key == key);
        return setting?.Value;
    }

    public async Task UpsertAsync(string key, string value, string? description = null)
    {
        var existing = await _dbSet.SingleOrDefaultAsync(bs => bs.Key == key);
        if (existing is null)
        {
            await _dbSet.AddAsync(new BotSetting
            {
                Key = key,
                Value = value,
                Description = description
            });
        }
        else
        {
            existing.Value = value;
            if (description is not null)
                existing.Description = description;
            _dbSet.Update(existing);
        }
    }
}
