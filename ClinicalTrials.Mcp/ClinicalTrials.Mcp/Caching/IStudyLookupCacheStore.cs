namespace ClinicalTrials.Mcp.Caching;

public interface IStudyLookupCacheStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken);
}
