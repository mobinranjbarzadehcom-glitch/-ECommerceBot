using ECommerceBot.API.Data;
using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBot.API.Infrastructure.Multitenancy;

public class TenantResolver : ITenantResolver
{
    private readonly AppDbContext _context;

    public TenantResolver(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> ResolveBySlugAsync(string slug, CancellationToken ct = default) =>
        await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantSlug == slug && t.IsActive, ct);

    public async Task<Tenant?> ResolveByIdAsync(int tenantId, CancellationToken ct = default) =>
        await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct);

    public async Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default) =>
        await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantSlug == slug, ct);
}
