using Microsoft.Extensions.Diagnostics.HealthChecks;
using ClinicalTrials.Mcp.Caching;
using ClinicalTrials.Mcp.RateLimiting;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Health;

internal sealed class RedisDependencyHealthCheck(
    IServiceProvider serviceProvider,
    IOptions<StudyLookupCacheSettings> cacheSettings,
    IOptions<McpRateLimitingSettings> rateLimitingSettings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (cacheSettings.Value.Provider != CacheProvider.Redis &&
            rateLimitingSettings.Value.Provider != RateLimitProvider.Redis)
        {
            return HealthCheckResult.Healthy("Redis dependency is not enabled for this runtime profile.");
        }

        try
        {
            var probe = serviceProvider.GetRequiredService<IRedisDependencyProbe>();
            await probe.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy("Redis dependency is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis dependency is unavailable.", ex);
        }
    }
}
