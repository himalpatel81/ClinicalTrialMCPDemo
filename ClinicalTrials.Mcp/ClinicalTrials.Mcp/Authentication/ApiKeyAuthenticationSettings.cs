namespace ClinicalTrials.Mcp.Authentication;

internal sealed class ApiKeyAuthenticationSettings
{
    public const string SectionName = "Authentication:ApiKeys";

    public string HeaderName { get; init; } = "X-Api-Key";

    public List<ApiKeyClientSettings> Clients { get; init; } = [];
}

internal sealed class ApiKeyClientSettings
{
    public string ClientId { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;
}
