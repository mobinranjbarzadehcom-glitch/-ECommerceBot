namespace ECommerceBot.API.Telegram.Services;

public interface IBotTextService
{
    /// <summary>Look up a key from BotSettings / defaults, no language suffix.</summary>
    Task<string> GetAsync(string key, string defaultValue = "");

    /// <summary>Look up a key with language fallback: tries {key}.{lang} then {key}.</summary>
    Task<string> GetAsync(string key, string lang, string defaultValue = "");

    /// <summary>Look up and substitute template variables, no language suffix.</summary>
    Task<string> FormatAsync(string key, Dictionary<string, string> vars, string defaultValue = "");

    /// <summary>Look up and substitute template variables with language fallback.</summary>
    Task<string> FormatAsync(string key, string lang, Dictionary<string, string> vars, string defaultValue = "");

    Task SetAsync(string key, string value);
}
