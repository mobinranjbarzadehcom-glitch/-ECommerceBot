namespace ECommerceBot.API.DTOs.Ticket;

public class TicketMessageDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsAdminMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
