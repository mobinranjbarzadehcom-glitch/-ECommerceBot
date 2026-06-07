namespace ECommerceBot.API.Entities;

public class TenantNote : BaseEntity
{
    public int TenantId { get; set; }
    public string Note { get; set; } = string.Empty;
    public long CreatedBySuperAdminId { get; set; }

    public Tenant? Tenant { get; set; }
}
