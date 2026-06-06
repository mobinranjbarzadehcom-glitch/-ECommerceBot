using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IBroadcastService
{
    /// <summary>
    /// Sends an HTML-formatted message to all non-blocked users who have a chat ID.
    /// Returns the number of successfully delivered messages.
    /// </summary>
    Task<ServiceResult<int>> SendBroadcastAsync(string htmlMessage, CancellationToken ct = default);
}
