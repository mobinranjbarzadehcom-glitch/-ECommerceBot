using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Entities;

public class TelegramUser : BaseEntity
{
    public long TelegramId { get; set; }
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool IsBlocked { get; set; } = false;
    public decimal WalletBalance { get; set; } = 0;
    public ConversationState CurrentState { get; set; } = ConversationState.None;
    public string? TempData { get; set; }
    public DateTime? LastActivity { get; set; }

    /// <summary>User's preferred display language. Default: fa (Persian).</summary>
    public string PreferredLanguage { get; set; } = "fa";

    public Cart? Cart { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
    public ICollection<Ticket> CreatedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketMessage> TicketMessages { get; set; } = new List<TicketMessage>();
}
