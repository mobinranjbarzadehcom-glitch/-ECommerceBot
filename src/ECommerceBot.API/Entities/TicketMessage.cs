namespace ECommerceBot.API.Entities;

public class TicketMessage : BaseEntity
{
    public int TicketId { get; set; }
    public int SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsAdminMessage { get; set; } = false;

    public Ticket Ticket { get; set; } = null!;
    public TelegramUser Sender { get; set; } = null!;
}
