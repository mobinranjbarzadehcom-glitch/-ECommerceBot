namespace ECommerceBot.API.DTOs.User;

public class CreateUserDto
{
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
}
