using Microsoft.Extensions.Caching.Distributed;

namespace ClinicalTrials.Mcp.Caching;

public sealed class DistributedStudyLookupCacheStore(IDistributedCache cache) : IStudyLookupCacheStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        cache.GetStringAsync(key, cancellationToken);

    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            key,
            value,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            cancellationToken);
}
