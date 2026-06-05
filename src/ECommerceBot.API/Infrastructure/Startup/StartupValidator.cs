namespace ECommerceBot.API.Infrastructure.Startup;

public static class StartupValidator
{
    public static void Validate(IConfiguration config, IHostEnvironment env, ILogger logger)
    {
        var issues = new List<(string Field, string Message)>();

        if (string.IsNullOrWhiteSpace(config["Telegram:BotToken"]))
            issues.Add(("Telegram:BotToken", "Bot token is not configured."));

        if (string.IsNullOrWhiteSpace(config["Telegram:WebhookSecretToken"]))
            issues.Add(("Telegram:WebhookSecretToken", "Webhook secret token is not configured. Webhook endpoint will accept any request."));

        if (string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
            issues.Add(("ConnectionStrings:DefaultConnection", "Database connection string is not configured."));

        var adminIds = config.GetSection("Telegram:AdminChatIds").Get<long[]>();
        if (adminIds is null || adminIds.Length == 0)
            issues.Add(("Telegram:AdminChatIds", "No admin chat IDs configured. Admin bot commands will not work."));

        if (issues.Count == 0) return;

        if (env.IsProduction())
        {
            foreach (var (field, msg) in issues)
                logger.LogCritical("Missing configuration: {Field} — {Message}", field, msg);

            throw new InvalidOperationException(
                $"Application cannot start: {issues.Count} required configuration value(s) are missing. Check startup logs.");
        }
        else
        {
            foreach (var (field, msg) in issues)
                logger.LogWarning("Configuration not set (non-fatal in {Env}): {Field} — {Message}",
                    env.EnvironmentName, field, msg);
        }
    }
}
