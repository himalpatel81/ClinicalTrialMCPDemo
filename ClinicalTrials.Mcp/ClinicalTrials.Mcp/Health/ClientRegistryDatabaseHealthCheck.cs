using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClinicalTrials.Mcp.Health;

internal sealed class ClientRegistryDatabaseHealthCheck(IClientRegistryDatabaseProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await probe.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy("Client registry database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Client registry database is unavailable.", ex);
        }
    }
}
