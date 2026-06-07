using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class ScheduledBroadcast : BaseEntity
{
    public int TenantId { get; set; }
    public string HtmlMessage { get; set; } = string.Empty;
    public BroadcastTargetFilter TargetFilter { get; set; } = BroadcastTargetFilter.All;
    public DateTime ScheduledAt { get; set; }
    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
