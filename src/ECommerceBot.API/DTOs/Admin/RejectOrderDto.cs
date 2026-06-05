namespace ECommerceBot.API.DTOs.Admin;

public class RejectOrderDto
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
