using ClinicalTrials.Mcp.Authentication;

namespace ClinicalTrials.Tests;

public sealed class ApiKeyProtectorTests
{
    [Fact]
    public void Create_GeneratesVerifiableApiKeyMaterial()
    {
        var material = ApiKeyProtector.Create();

        Assert.StartsWith("ctmcp_", material.RawApiKey, StringComparison.Ordinal);
        Assert.Equal(material.Prefix, ApiKeyProtector.GetLookupPrefix(material.RawApiKey));
        Assert.True(
            ApiKeyProtector.Verify(
                material.RawApiKey,
                material.SaltBase64,
                material.HashBase64,
                material.HashIterations,
                material.HashAlgorithm));
    }

    [Fact]
    public void GetLookupPrefix_ForLegacyKey_UsesDeterministicHashPrefix()
    {
        var prefix = ApiKeyProtector.GetLookupPrefix("clinical-trials-local-dev-key");

        Assert.Equal("52A73A82AE88D094", prefix);
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongApiKey()
    {
        var material = ApiKeyProtector.Create();

        Assert.False(
            ApiKeyProtector.Verify(
                material.RawApiKey + "-wrong",
                material.SaltBase64,
                material.HashBase64,
                material.HashIterations,
                material.HashAlgorithm));
    }
}
