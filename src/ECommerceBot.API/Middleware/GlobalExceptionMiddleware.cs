using System.Text.Json;

namespace ECommerceBot.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path} — TraceId: {TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted) return;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var payload = new
        {
            error = "An internal server error occurred.",
            traceId = context.TraceIdentifier,
            // Only include details in Development to avoid information leakage
            detail = _env.IsDevelopment() ? ex.Message : null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
