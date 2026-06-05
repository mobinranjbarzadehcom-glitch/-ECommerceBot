using ECommerceBot.API.Telegram.Services;

namespace ECommerceBot.API.Infrastructure.Localization;

/// <summary>
/// Resolves localised bot text using language-specific BotSetting keys.
/// Key lookup order for language "en":
///   1. BotSettings["WelcomeMessage.en"]
///   2. BotSettings["WelcomeMessage.fa"]  (Persian fallback)
///   3. BotSettings["WelcomeMessage"]     (language-neutral fallback)
///   4. defaultValue
/// Default language is Persian ("fa").
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IBotTextService _texts;

    public LocalizationService(IBotTextService texts)
    {
        _texts = texts;
    }

    public async Task<string> GetAsync(string key, string language, string defaultValue = "")
    {
        language = NormalizeLanguage(language);

        if (!string.Equals(language, "fa", StringComparison.OrdinalIgnoreCase))
        {
            // Try language-specific key first
            var langValue = await _texts.GetAsync($"{key}.{language}", string.Empty);
            if (!string.IsNullOrEmpty(langValue)) return langValue;

            // Fallback: Persian
            var faValue = await _texts.GetAsync($"{key}.fa", string.Empty);
            if (!string.IsNullOrEmpty(faValue)) return faValue;
        }
        else
        {
            // Persian-first: try key.fa then base key
            var faValue = await _texts.GetAsync($"{key}.fa", string.Empty);
            if (!string.IsNullOrEmpty(faValue)) return faValue;
        }

        // Language-neutral base key
        var baseValue = await _texts.GetAsync(key, defaultValue);
        return string.IsNullOrEmpty(baseValue) ? defaultValue : baseValue;
    }

    public async Task<string> FormatAsync(string key, string language, Dictionary<string, string> vars, string defaultValue = "")
    {
        var template = await GetAsync(key, language, defaultValue);
        foreach (var (k, v) in vars)
            template = template.Replace($"{{{k}}}", v);
        return template;
    }

    private static string NormalizeLanguage(string? lang) =>
        string.IsNullOrWhiteSpace(lang) ? "fa" : lang.ToLowerInvariant().Trim();
}
