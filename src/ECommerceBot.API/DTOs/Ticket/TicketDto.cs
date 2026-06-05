using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.Ticket;

public class TicketDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? AssignedAdminId { get; set; }
    public int? RelatedOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TicketMessageDto> Messages { get; set; } = new();
}
