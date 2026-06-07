using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Infrastructure.Multitenancy;

public interface ITenantResolver
{
    Task<Tenant?> ResolveBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> ResolveByIdAsync(int tenantId, CancellationToken ct = default);
    /// <summary>Finds a tenant by slug regardless of IsActive — for diagnostics only.</summary>
    Task<Tenant?> FindBySlugAsync(string slug, CancellationToken ct = default);
}
