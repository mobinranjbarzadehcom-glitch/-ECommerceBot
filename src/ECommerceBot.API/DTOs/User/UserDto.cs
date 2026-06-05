using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.User;

public class UserDto
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; }
    public bool IsBlocked { get; set; }
    public decimal WalletBalance { get; set; }
    public DateTime CreatedAt { get; set; }
}
