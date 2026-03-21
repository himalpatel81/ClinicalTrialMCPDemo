namespace ClinicalTrials.Mcp.RateLimiting;

public interface IMcpRateLimitStore
{
    Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}
