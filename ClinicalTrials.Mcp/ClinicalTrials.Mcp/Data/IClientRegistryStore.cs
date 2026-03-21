namespace ClinicalTrials.Mcp.Data;

public interface IClientRegistryStore
{
    Task<IReadOnlyList<ApiKeyAuthenticationRecord>> GetAuthenticationCandidatesAsync(string keyPrefix, CancellationToken cancellationToken);

    Task<ClientRegistryClient?> GetClientByCodeAsync(string clientCode, CancellationToken cancellationToken);

    Task CreateClientAsync(ClientRegistryClient client, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClientRegistryApiKey>> GetApiKeysByClientCodeAsync(string clientCode, CancellationToken cancellationToken);

    Task AddApiKeyAsync(ClientRegistryApiKey apiKey, CancellationToken cancellationToken);

    Task RevokeApiKeyAsync(Guid apiKeyId, DateTimeOffset revokedUtc, Guid? replacedByApiKeyId, CancellationToken cancellationToken);

    Task DeactivateClientAsync(string clientCode, DateTimeOffset deactivatedUtc, CancellationToken cancellationToken);
}
