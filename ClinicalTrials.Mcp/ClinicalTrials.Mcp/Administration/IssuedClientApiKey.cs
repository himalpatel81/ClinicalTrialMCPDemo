namespace ClinicalTrials.Mcp.Administration;

public sealed record IssuedClientApiKey(
    string ClientCode,
    Guid ApiKeyId,
    string KeyLabel,
    string KeyPrefix,
    string RawApiKey,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc);
