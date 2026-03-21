namespace ClinicalTrials.Mcp.Caching;

public sealed class StudyLookupCacheSettings
{
    public const string SectionName = "Caching";

    public CacheProvider Provider { get; init; } = CacheProvider.Redis;

    public int SuccessTtlSeconds { get; init; } = 300;

    public int NotFoundTtlSeconds { get; init; } = 60;
}
