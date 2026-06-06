namespace ECommerceBot.API.Services.Interfaces;

public interface IAiSupportService
{
    /// <summary>
    /// Returns a configured auto-reply message for the given user message, or null if not configured.
    /// Only called when the tenant plan has AllowsAiSupport = true.
    /// </summary>
    Task<string?> GetAutoReplyAsync(string userMessage);
}
