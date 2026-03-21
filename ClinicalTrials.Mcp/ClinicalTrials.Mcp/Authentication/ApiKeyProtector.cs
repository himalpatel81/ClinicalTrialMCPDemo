using System.Security.Cryptography;
using System.Text;

namespace ClinicalTrials.Mcp.Authentication;

public static class ApiKeyProtector
{
    public const string DefaultHashAlgorithm = "PBKDF2-SHA512";
    public const int DefaultHashIterations = 120_000;
    public const int DefaultPrefixLength = 16;
    private const string IssuedKeyPrefix = "ctmcp";

    public static ApiKeyMaterial Create(int hashIterations = DefaultHashIterations)
    {
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var rawApiKey = $"{IssuedKeyPrefix}_{prefix}_{secret}";

        return Protect(rawApiKey, hashIterations, prefix);
    }

    public static ApiKeyMaterial Protect(
        string rawApiKey,
        int hashIterations = DefaultHashIterations,
        string? explicitPrefix = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawApiKey);

        var prefix = explicitPrefix ?? GetLookupPrefix(rawApiKey);
        var saltBytes = RandomNumberGenerator.GetBytes(32);
        var hashBytes = Hash(rawApiKey, saltBytes, hashIterations);

        return new ApiKeyMaterial(
            Prefix: prefix,
            RawApiKey: rawApiKey,
            HashAlgorithm: DefaultHashAlgorithm,
            HashIterations: hashIterations,
            SaltBase64: Convert.ToBase64String(saltBytes),
            HashBase64: Convert.ToBase64String(hashBytes));
    }

    public static string GetLookupPrefix(string rawApiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawApiKey);

        if (TryGetIssuedKeyPrefix(rawApiKey, out var parsedPrefix))
        {
            return parsedPrefix;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey));
        return Convert.ToHexString(digest[..8]);
    }

    public static bool Verify(
        string rawApiKey,
        string saltBase64,
        string hashBase64,
        int hashIterations,
        string hashAlgorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawApiKey);

        if (!string.Equals(hashAlgorithm, DefaultHashAlgorithm, StringComparison.Ordinal))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(saltBase64);
        var expectedHashBytes = Convert.FromBase64String(hashBase64);
        var computedHashBytes = Hash(rawApiKey, saltBytes, hashIterations);

        return CryptographicOperations.FixedTimeEquals(expectedHashBytes, computedHashBytes);
    }

    private static bool TryGetIssuedKeyPrefix(string rawApiKey, out string prefix)
    {
        prefix = string.Empty;

        var segments = rawApiKey.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 3 ||
            !string.Equals(segments[0], IssuedKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        prefix = segments[1];
        return true;
    }

    private static byte[] Hash(string rawApiKey, byte[] saltBytes, int hashIterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(rawApiKey),
            saltBytes,
            hashIterations,
            HashAlgorithmName.SHA512,
            32);
}
