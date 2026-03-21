namespace ClinicalTrials.Mcp.Data;

public sealed record ClientRegistryClient(
    Guid ClientId,
    string ClientCode,
    string DisplayName,
    bool IsActive,
    int? PermitLimitOverride,
    int? CacheTtlSecondsOverride,
    int? NegativeCacheTtlSecondsOverride,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DateTimeOffset? DeactivatedUtc);
