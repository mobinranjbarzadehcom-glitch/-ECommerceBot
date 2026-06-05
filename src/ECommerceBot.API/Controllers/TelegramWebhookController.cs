using ECommerceBot.API.Telegram;
using ECommerceBot.API.Telegram.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;

namespace ECommerceBot.API.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly IUpdateDispatcher _dispatcher;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        IUpdateDispatcher dispatcher,
        IOptions<TelegramOptions> options,
        ILogger<TelegramWebhookController> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [EnableRateLimiting("webhook")]
    public IActionResult Webhook([FromBody] Update update, CancellationToken ct)
    {
        // Validate secret token
        if (!string.IsNullOrEmpty(_options.WebhookSecretToken))
        {
            var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
            if (header != _options.WebhookSecretToken)
            {
                _logger.LogWarning("Invalid webhook secret token from {IP}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }
        }

        // Fire-and-forget: return 200 immediately, dispatch in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _dispatcher.DispatchAsync(update, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook update {UpdateId}", update?.Id);
            }
        }, CancellationToken.None);

        return Ok();
    }
}
