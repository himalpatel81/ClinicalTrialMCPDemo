namespace ClinicalTrials.Mcp.Data;

public sealed record ApiKeyAuthenticationRecord(
    Guid ClientId,
    string ClientCode,
    string DisplayName,
    bool ClientIsActive,
    int? PermitLimitOverride,
    Guid ApiKeyId,
    string KeyLabel,
    string KeyPrefix,
    string HashAlgorithm,
    int HashIterations,
    string SaltBase64,
    string HashBase64,
    bool ApiKeyIsActive,
    DateTimeOffset? ExpiresUtc,
    DateTimeOffset? RevokedUtc)
{
    public bool IsCurrentlyActive(DateTimeOffset nowUtc) =>
        ClientIsActive
        && ApiKeyIsActive
        && RevokedUtc is null
        && (ExpiresUtc is null || ExpiresUtc > nowUtc);
}
