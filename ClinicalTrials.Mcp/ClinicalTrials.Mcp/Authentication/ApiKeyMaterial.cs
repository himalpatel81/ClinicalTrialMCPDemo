namespace ClinicalTrials.Mcp.Authentication;

public sealed record ApiKeyMaterial(
    string Prefix,
    string RawApiKey,
    string HashAlgorithm,
    int HashIterations,
    string SaltBase64,
    string HashBase64);
