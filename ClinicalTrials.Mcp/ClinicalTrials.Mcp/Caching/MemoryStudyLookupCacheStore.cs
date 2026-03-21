using Microsoft.Extensions.Caching.Memory;

namespace ClinicalTrials.Mcp.Caching;

public sealed class MemoryStudyLookupCacheStore(IMemoryCache cache) : IStudyLookupCacheStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        cache.TryGetValue(key, out string? value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }
}
