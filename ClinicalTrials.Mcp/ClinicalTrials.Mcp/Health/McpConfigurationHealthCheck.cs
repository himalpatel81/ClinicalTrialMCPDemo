using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Health;

internal sealed class McpConfigurationHealthCheck(
    IOptions<ApiKeyAuthenticationSettings> apiKeySettings,
    IOptions<StudyServiceSettings> studyServiceSettings)
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

        if (settings.Clients.Count == 0)
        {
            failures.Add("Authentication:ApiKeys:Clients must contain at least one configured client.");
        }

        return Task.FromResult(
            failures.Count == 0
                ? HealthCheckResult.Healthy("Configuration is ready.")
                : HealthCheckResult.Unhealthy(string.Join(' ', failures)));
    }
}
