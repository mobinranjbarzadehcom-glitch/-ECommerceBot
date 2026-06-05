using ECommerceBot.API.Infrastructure.Licensing;
using Microsoft.Extensions.Options;

namespace ECommerceBot.API.Infrastructure.Background;

public class LicenseValidationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LicenseStatusCache _cache;
    private readonly LicenseOptions _options;
    private readonly ILogger<LicenseValidationBackgroundService> _logger;

    public LicenseValidationBackgroundService(
        IServiceScopeFactory scopeFactory,
        LicenseStatusCache cache,
        IOptions<LicenseOptions> options,
        ILogger<LicenseValidationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("License validation service disabled (License:Enabled=false).");
            return;
        }

        _logger.LogInformation("License validation service started. Interval: {Interval} min",
            _options.ValidateIntervalMinutes);

        // Initial validation shortly after startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        await RunValidationAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_options.ValidateIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunValidationAsync(stoppingToken);
        }
    }

    private async Task RunValidationAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseService>();
            var result = await licenseService.ValidateAsync(ct);

            _logger.LogInformation("License validation: {Status} — {Message}", result.Status, result.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License validation encountered an error.");
        }
    }
}
