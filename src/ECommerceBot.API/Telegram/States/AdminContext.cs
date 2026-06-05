namespace ECommerceBot.API.Telegram.States;

public class AdminContext
{
    public int? TargetOrderId { get; set; }
    public int? TargetUserId { get; set; }
    public int? TargetCategoryId { get; set; }
    public int? TargetProductId { get; set; }
    public int? TargetCardId { get; set; }
    public string? TargetSettingKey { get; set; }
    public string? PendingAction { get; set; }
}
