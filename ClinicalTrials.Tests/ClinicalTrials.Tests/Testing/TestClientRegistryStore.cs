using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Data;

namespace ClinicalTrials.Tests.Testing;

internal sealed class TestClientRegistryStore : IClientRegistryStore
{
    private readonly List<ClientRegistryClient> clients;
    private readonly List<ClientRegistryApiKey> apiKeys;

    public TestClientRegistryStore(
        IEnumerable<ClientRegistryClient>? clients = null,
        IEnumerable<ClientRegistryApiKey>? apiKeys = null)
    {
        this.clients = clients?.ToList() ?? [];
        this.apiKeys = apiKeys?.ToList() ?? [];
    }

    public static TestClientRegistryStore CreateDefault(
        string rawApiKey,
        bool clientIsActive = true,
        int? permitLimitOverride = null)
    {
        var material = ApiKeyProtector.Protect(rawApiKey);
        var client = new ClientRegistryClient(
            ClientId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ClientCode: "integration-test-client",
            DisplayName: "Integration Test Client",
            IsActive: clientIsActive,
            PermitLimitOverride: permitLimitOverride,
            CacheTtlSecondsOverride: null,
            NegativeCacheTtlSecondsOverride: null,
            CreatedUtc: DateTimeOffset.UtcNow,
            UpdatedUtc: DateTimeOffset.UtcNow,
            DeactivatedUtc: clientIsActive ? null : DateTimeOffset.UtcNow);
        var apiKey = new ClientRegistryApiKey(
            ApiKeyId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ClientId: client.ClientId,
            ClientCode: client.ClientCode,
            KeyLabel: "integration",
            KeyPrefix: material.Prefix,
            HashAlgorithm: material.HashAlgorithm,
            HashIterations: material.HashIterations,
            SaltBase64: material.SaltBase64,
            HashBase64: material.HashBase64,
            IsActive: true,
            CreatedUtc: DateTimeOffset.UtcNow,
            ExpiresUtc: null,
            RevokedUtc: null,
            ReplacedByApiKeyId: null);

        return new TestClientRegistryStore([client], [apiKey]);
    }

    public Task<IReadOnlyList<ApiKeyAuthenticationRecord>> GetAuthenticationCandidatesAsync(string keyPrefix, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiKeyAuthenticationRecord> results = apiKeys
            .Where(record => string.Equals(record.KeyPrefix, keyPrefix, StringComparison.Ordinal))
            .Join(
                clients,
                key => key.ClientId,
                client => client.ClientId,
                (key, client) => new ApiKeyAuthenticationRecord(
                    ClientId: client.ClientId,
                    ClientCode: client.ClientCode,
                    DisplayName: client.DisplayName,
                    ClientIsActive: client.IsActive,
                    PermitLimitOverride: client.PermitLimitOverride,
                    ApiKeyId: key.ApiKeyId,
                    KeyLabel: key.KeyLabel,
                    KeyPrefix: key.KeyPrefix,
                    HashAlgorithm: key.HashAlgorithm,
                    HashIterations: key.HashIterations,
                    SaltBase64: key.SaltBase64,
                    HashBase64: key.HashBase64,
                    ApiKeyIsActive: key.IsActive,
                    ExpiresUtc: key.ExpiresUtc,
                    RevokedUtc: key.RevokedUtc))
            .ToArray();

        return Task.FromResult(results);
    }

    public Task<ClientRegistryClient?> GetClientByCodeAsync(string clientCode, CancellationToken cancellationToken)
    {
        var client = clients.SingleOrDefault(client => string.Equals(client.ClientCode, clientCode, StringComparison.Ordinal));
        return Task.FromResult(client);
    }

    public Task CreateClientAsync(ClientRegistryClient client, CancellationToken cancellationToken)
    {
        clients.Add(client);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ClientRegistryApiKey>> GetApiKeysByClientCodeAsync(string clientCode, CancellationToken cancellationToken)
    {
        IReadOnlyList<ClientRegistryApiKey> results = apiKeys
            .Where(key => string.Equals(key.ClientCode, clientCode, StringComparison.Ordinal))
            .ToArray();

        return Task.FromResult(results);
    }

    public Task AddApiKeyAsync(ClientRegistryApiKey apiKey, CancellationToken cancellationToken)
    {
        apiKeys.Add(apiKey);
        return Task.CompletedTask;
    }

    public Task RevokeApiKeyAsync(Guid apiKeyId, DateTimeOffset revokedUtc, Guid? replacedByApiKeyId, CancellationToken cancellationToken)
    {
        var index = apiKeys.FindIndex(key => key.ApiKeyId == apiKeyId);
        if (index >= 0)
        {
            apiKeys[index] = apiKeys[index] with
            {
                IsActive = false,
                RevokedUtc = revokedUtc,
                ReplacedByApiKeyId = replacedByApiKeyId
            };
        }

        return Task.CompletedTask;
    }

    public Task DeactivateClientAsync(string clientCode, DateTimeOffset deactivatedUtc, CancellationToken cancellationToken)
    {
        var index = clients.FindIndex(client => string.Equals(client.ClientCode, clientCode, StringComparison.Ordinal));
        if (index >= 0)
        {
            clients[index] = clients[index] with
            {
                IsActive = false,
                UpdatedUtc = deactivatedUtc,
                DeactivatedUtc = deactivatedUtc
            };
        }

        return Task.CompletedTask;
    }
}
