using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Repositories.Implementations;

public class TenantNoteRepository : GenericRepository<TenantNote>, ITenantNoteRepository
{
    public TenantNoteRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<TenantNote>> GetByTenantIdAsync(int tenantId) =>
        await _dbSet
            .Where(n => n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
}
