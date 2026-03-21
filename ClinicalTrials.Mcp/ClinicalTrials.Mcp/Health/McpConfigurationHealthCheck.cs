using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Caching;
using ClinicalTrials.Mcp.Data;
using ClinicalTrials.Mcp.RateLimiting;
using ClinicalTrials.Mcp.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Health;

internal sealed class McpConfigurationHealthCheck(
    IOptions<ApiKeyAuthenticationSettings> apiKeySettings,
    IOptions<StudyServiceSettings> studyServiceSettings,
    IOptions<ClientRegistryDatabaseSettings> clientRegistrySettings,
    IOptions<StudyLookupCacheSettings> cacheSettings,
    IOptions<McpRateLimitingSettings> rateLimitingSettings,
    IConfiguration configuration)
    : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        var studyService = studyServiceSettings.Value;
        if (!Uri.TryCreate(studyService.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add("StudyService:BaseUrl is missing or invalid.");
        }

        if (string.IsNullOrWhiteSpace(studyService.StudyLookupPathTemplate) ||
            !studyService.StudyLookupPathTemplate.Contains(StudyServiceSettings.NctIdPlaceholder, StringComparison.Ordinal))
        {
            failures.Add("StudyService:StudyLookupPathTemplate must contain '{nctId}'.");
        }

        var settings = apiKeySettings.Value;
        if (string.IsNullOrWhiteSpace(settings.HeaderName))
        {
            failures.Add("Authentication:ApiKeys:HeaderName is missing.");
        }

        if (string.IsNullOrWhiteSpace(clientRegistrySettings.Value.ConnectionString))
        {
            failures.Add("ConnectionStrings:ClientRegistry is missing.");
        }

        if (cacheSettings.Value.Provider == CacheProvider.Redis &&
            string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            failures.Add("ConnectionStrings:Redis is required when Caching:Provider is Redis.");
        }

        if (rateLimitingSettings.Value.Provider == RateLimitProvider.Redis &&
            string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            failures.Add("ConnectionStrings:Redis is required when RateLimiting:Provider is Redis.");
        }

        return Task.FromResult(
            failures.Count == 0
                ? HealthCheckResult.Healthy("Configuration is ready.")
                : HealthCheckResult.Unhealthy(string.Join(' ', failures)));
    }
}
