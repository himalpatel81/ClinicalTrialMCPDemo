namespace ClinicalTrials.Mcp.RateLimiting;

public sealed class McpRateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public RateLimitProvider Provider { get; init; } = RateLimitProvider.Redis;

    public int WindowSeconds { get; init; } = 60;

    public int PerClientPermitLimit { get; init; } = 60;

    public int GlobalPermitLimit { get; init; } = 600;
}
