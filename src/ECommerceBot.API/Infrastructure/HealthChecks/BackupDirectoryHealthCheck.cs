using ECommerceBot.API.Infrastructure.Backup;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ECommerceBot.API.Infrastructure.HealthChecks;

public class BackupDirectoryHealthCheck : IHealthCheck
{
    private readonly BackupOptions _options;

    public BackupDirectoryHealthCheck(IOptions<BackupOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Backup is disabled."));

        try
        {
            Directory.CreateDirectory(_options.Directory);
            var probe = Path.Combine(_options.Directory, ".healthcheck_probe");
            File.WriteAllText(probe, DateTime.UtcNow.ToString("O"));
            File.Delete(probe);

            return Task.FromResult(
                HealthCheckResult.Healthy($"Backup directory '{_options.Directory}' is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Backup directory '{_options.Directory}' is not accessible.", ex));
        }
    }
}
