namespace ClinicalTrials.Mcp.Runtime;

public sealed class McpRequestProtectionSettings
{
    public const string SectionName = "RequestProtection";

    public int RequestTimeoutSeconds { get; init; } = 30;

    public long MaxRequestBodySizeBytes { get; init; } = 1024 * 1024;
}
