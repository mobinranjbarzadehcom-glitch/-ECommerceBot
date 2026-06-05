namespace ECommerceBot.API.Infrastructure.Security;

/// <summary>
/// Encodes user-supplied text before embedding it inside HTML Telegram messages.
/// This prevents HTML injection while preserving intentional HTML in bot-authored templates.
/// </summary>
public static class HtmlSanitizer
{
    /// <summary>
    /// HTML-encodes a user-provided string so it renders as literal text in Telegram HTML messages.
    /// Does NOT encode bot-authored HTML templates or <tg-emoji> tags.
    /// </summary>
    public static string Encode(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    /// <summary>
    /// Returns the input unchanged. Use for values that are safe integers, decimals, or bot-generated keys.
    /// </summary>
    public static string Passthrough(string? input) => input ?? string.Empty;
}
