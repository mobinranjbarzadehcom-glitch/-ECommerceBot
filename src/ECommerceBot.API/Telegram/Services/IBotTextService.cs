namespace ECommerceBot.API.Telegram.Services;

public interface IBotTextService
{
    Task<string> GetAsync(string key, string defaultValue = "");
    Task<string> FormatAsync(string key, Dictionary<string, string> vars, string defaultValue = "");
    Task SetAsync(string key, string value);
}
