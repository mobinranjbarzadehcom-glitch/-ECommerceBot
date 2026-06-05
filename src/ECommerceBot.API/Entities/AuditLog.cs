namespace ECommerceBot.API.Entities;

public class AuditLog : BaseEntity
{
    public int AdminId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public int? TargetId { get; set; }
    public string? Details { get; set; }

    public TelegramUser Admin { get; set; } = null!;
}
