namespace ClinicalTrials.Mcp.Data;

public sealed record ClientRegistryApiKey(
    Guid ApiKeyId,
    Guid ClientId,
    string ClientCode,
    string KeyLabel,
    string KeyPrefix,
    string HashAlgorithm,
    int HashIterations,
    string SaltBase64,
    string HashBase64,
    bool IsActive,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc,
    DateTimeOffset? RevokedUtc,
    Guid? ReplacedByApiKeyId);
