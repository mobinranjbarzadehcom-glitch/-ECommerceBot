namespace ECommerceBot.API.DTOs.Ticket;

public class CreateTicketDto
{
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RelatedOrderId { get; set; }
}
