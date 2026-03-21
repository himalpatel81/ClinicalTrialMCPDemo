using ClinicalTrials.Mcp.Administration;
using ClinicalTrials.Tests.Testing;

namespace ClinicalTrials.Tests;

public sealed class ClientRegistryAdminServiceTests
{
    [Fact]
    public async Task CreateClientAsync_PersistsClientRecord()
    {
        var store = new TestClientRegistryStore();
        var service = new ClientRegistryAdminService(store, TimeProvider.System);

        await service.CreateClientAsync(
            clientCode: "new-client",
            displayName: "New Client",
            permitLimitOverride: 42,
            cacheTtlSecondsOverride: 120,
            negativeCacheTtlSecondsOverride: 30,
            cancellationToken: CancellationToken.None);

        var client = await store.GetClientByCodeAsync("new-client", CancellationToken.None);

        Assert.NotNull(client);
        Assert.Equal("New Client", client.DisplayName);
        Assert.Equal(42, client.PermitLimitOverride);
    }

    [Fact]
    public async Task RotateApiKeyAsync_RevokesExistingActiveKeys()
    {
        var store = TestClientRegistryStore.CreateDefault("integration-test-api-key");
        var service = new ClientRegistryAdminService(store, TimeProvider.System);

        var issuedKey = await service.RotateApiKeyAsync(
            clientCode: "integration-test-client",
            keyLabel: "rotated",
            expiresUtc: null,
            cancellationToken: CancellationToken.None);

        var keys = await store.GetApiKeysByClientCodeAsync("integration-test-client", CancellationToken.None);

        Assert.Contains(keys, key => key.ApiKeyId == issuedKey.ApiKeyId && key.IsActive);
        Assert.Contains(keys, key => key.ApiKeyId != issuedKey.ApiKeyId && !key.IsActive && key.RevokedUtc.HasValue);
        Assert.StartsWith("ctmcp_", issuedKey.RawApiKey, StringComparison.Ordinal);
    }
}
