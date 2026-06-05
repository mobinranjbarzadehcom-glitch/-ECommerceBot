namespace ECommerceBot.API.DTOs.Admin;

public class AdminNoteDto
{
    public int OrderId { get; set; }
    public string Note { get; set; } = string.Empty;
}
