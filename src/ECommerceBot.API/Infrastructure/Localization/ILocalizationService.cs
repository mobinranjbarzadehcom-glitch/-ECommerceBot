namespace ECommerceBot.API.Infrastructure.Localization;

public interface ILocalizationService
{
    /// <summary>Get text for key in given language, with fallback to Persian then default.</summary>
    Task<string> GetAsync(string key, string language, string defaultValue = "");

    /// <summary>Get text with variable substitution.</summary>
    Task<string> FormatAsync(string key, string language, Dictionary<string, string> vars, string defaultValue = "");
}
