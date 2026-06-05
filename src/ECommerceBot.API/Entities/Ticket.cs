using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class Ticket : BaseEntity
{
    public int UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public int? AssignedAdminId { get; set; }
    public int? RelatedOrderId { get; set; }

    public TelegramUser User { get; set; } = null!;
    public TelegramUser? AssignedAdmin { get; set; }
    public Order? RelatedOrder { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}
