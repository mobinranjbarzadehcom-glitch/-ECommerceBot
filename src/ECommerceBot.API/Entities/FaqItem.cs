namespace ECommerceBot.API.Entities;

public class FaqItem : BaseEntity
{
    public int TenantId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
