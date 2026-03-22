namespace ClinicalTrials.Mcp.Health;

public sealed class SecretsSettings
{
    public const string SectionName = "Secrets";

    public SecretsProvider Provider { get; init; } = SecretsProvider.Local;

    public AzureKeyVaultSettings AzureKeyVault { get; init; } = new();
}

public enum SecretsProvider
{
    Local = 0,
    AzureKeyVault = 1
}

public sealed class AzureKeyVaultSettings
{
    public string VaultUri { get; init; } = string.Empty;
}
