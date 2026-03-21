using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Data;

namespace ClinicalTrials.Mcp.Administration;

public sealed class ClientRegistryAdminService(
    IClientRegistryStore clientRegistryStore,
    TimeProvider timeProvider)
{
    public async Task CreateClientAsync(
        string clientCode,
        string displayName,
        int? permitLimitOverride,
        int? cacheTtlSecondsOverride,
        int? negativeCacheTtlSecondsOverride,
        CancellationToken cancellationToken)
    {
        var existing = await clientRegistryStore.GetClientByCodeAsync(clientCode, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A client with code '{clientCode}' already exists.");
        }

        var nowUtc = timeProvider.GetUtcNow();
        var client = new ClientRegistryClient(
            ClientId: Guid.NewGuid(),
            ClientCode: clientCode,
            DisplayName: displayName,
            IsActive: true,
            PermitLimitOverride: permitLimitOverride,
            CacheTtlSecondsOverride: cacheTtlSecondsOverride,
            NegativeCacheTtlSecondsOverride: negativeCacheTtlSecondsOverride,
            CreatedUtc: nowUtc,
            UpdatedUtc: nowUtc,
            DeactivatedUtc: null);

        await clientRegistryStore.CreateClientAsync(client, cancellationToken);
    }

    public Task<IssuedClientApiKey> IssueApiKeyAsync(
        string clientCode,
        string keyLabel,
        DateTimeOffset? expiresUtc,
        CancellationToken cancellationToken) =>
        IssueApiKeyCoreAsync(clientCode, keyLabel, expiresUtc, revokeExisting: false, cancellationToken);

    public Task<IssuedClientApiKey> RotateApiKeyAsync(
        string clientCode,
        string keyLabel,
        DateTimeOffset? expiresUtc,
        CancellationToken cancellationToken) =>
        IssueApiKeyCoreAsync(clientCode, keyLabel, expiresUtc, revokeExisting: true, cancellationToken);

    public Task RevokeApiKeyAsync(Guid apiKeyId, CancellationToken cancellationToken) =>
        clientRegistryStore.RevokeApiKeyAsync(apiKeyId, timeProvider.GetUtcNow(), replacedByApiKeyId: null, cancellationToken);

    public Task DeactivateClientAsync(string clientCode, CancellationToken cancellationToken) =>
        clientRegistryStore.DeactivateClientAsync(clientCode, timeProvider.GetUtcNow(), cancellationToken);

    private async Task<IssuedClientApiKey> IssueApiKeyCoreAsync(
        string clientCode,
        string keyLabel,
        DateTimeOffset? expiresUtc,
        bool revokeExisting,
        CancellationToken cancellationToken)
    {
        var client = await clientRegistryStore.GetClientByCodeAsync(clientCode, cancellationToken)
            ?? throw new InvalidOperationException($"Client '{clientCode}' does not exist.");

        if (!client.IsActive)
        {
            throw new InvalidOperationException($"Client '{clientCode}' is inactive.");
        }

        var material = ApiKeyProtector.Create();
        var nowUtc = timeProvider.GetUtcNow();
        var apiKey = new ClientRegistryApiKey(
            ApiKeyId: Guid.NewGuid(),
            ClientId: client.ClientId,
            ClientCode: client.ClientCode,
            KeyLabel: keyLabel,
            KeyPrefix: material.Prefix,
            HashAlgorithm: material.HashAlgorithm,
            HashIterations: material.HashIterations,
            SaltBase64: material.SaltBase64,
            HashBase64: material.HashBase64,
            IsActive: true,
            CreatedUtc: nowUtc,
            ExpiresUtc: expiresUtc,
            RevokedUtc: null,
            ReplacedByApiKeyId: null);

        var existingKeys = revokeExisting
            ? await clientRegistryStore.GetApiKeysByClientCodeAsync(clientCode, cancellationToken)
            : [];

        await clientRegistryStore.AddApiKeyAsync(apiKey, cancellationToken);

        if (revokeExisting)
        {
            var activeKeys = existingKeys.Where(key =>
                key.IsActive &&
                key.RevokedUtc is null &&
                key.ApiKeyId != apiKey.ApiKeyId);

            foreach (var existingKey in activeKeys)
            {
                await clientRegistryStore.RevokeApiKeyAsync(
                    existingKey.ApiKeyId,
                    nowUtc,
                    apiKey.ApiKeyId,
                    cancellationToken);
            }
        }

        return new IssuedClientApiKey(
            ClientCode: client.ClientCode,
            ApiKeyId: apiKey.ApiKeyId,
            KeyLabel: apiKey.KeyLabel,
            KeyPrefix: apiKey.KeyPrefix,
            RawApiKey: material.RawApiKey,
            CreatedUtc: apiKey.CreatedUtc,
            ExpiresUtc: apiKey.ExpiresUtc);
    }
}
