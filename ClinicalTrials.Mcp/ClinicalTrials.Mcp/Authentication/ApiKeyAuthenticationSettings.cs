namespace ClinicalTrials.Mcp.Authentication;

internal sealed class ApiKeyAuthenticationSettings
{
    public const string SectionName = "Authentication:ApiKeys";

    public string HeaderName { get; init; } = "X-Api-Key";
}
