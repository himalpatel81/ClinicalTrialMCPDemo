using System.Net;
using System.Text.Json;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Shared;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Caching;

public sealed class CachedStudyLookupEndpointClient(
    StudyServiceHttpClient innerClient,
    IStudyLookupCacheStore cacheStore,
    IOptions<StudyLookupCacheSettings> settings)
    : IStudyLookupEndpointClient
{
    private readonly StudyLookupCacheSettings settings = settings.Value;

    public async Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
    {
        var cacheKey = $"study:{nctId.Trim().ToUpperInvariant()}";
        var cachedValue = await cacheStore.GetAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedValue))
        {
            return JsonSerializer.Deserialize<CachedStudyLookupResult>(cachedValue)!.ToStudyLookupResult();
        }

        var result = await innerClient.GetStudyAsync(nctId, cancellationToken);
        var ttl = GetTtl(result);
        if (ttl is null)
        {
            return result;
        }

        var cachePayload = JsonSerializer.Serialize(CachedStudyLookupResult.From(result));
        await cacheStore.SetAsync(cacheKey, cachePayload, ttl.Value, cancellationToken);

        return result;
    }

    private TimeSpan? GetTtl(StudyLookupResult result)
    {
        if (result.HasFailure || result.StatusCode is null)
        {
            return null;
        }

        if (result.StatusCode == HttpStatusCode.NotFound && settings.NotFoundTtlSeconds > 0)
        {
            return TimeSpan.FromSeconds(settings.NotFoundTtlSeconds);
        }

        if ((int)result.StatusCode >= 200 && (int)result.StatusCode <= 299)
        {
            return TimeSpan.FromSeconds(settings.SuccessTtlSeconds);
        }

        return null;
    }

    private sealed record CachedStudyLookupResult(
        int StatusCode,
        string ContentType,
        string Body)
    {
        public static CachedStudyLookupResult From(StudyLookupResult result) =>
            new((int)result.StatusCode!, result.ContentType, result.Body);

        public StudyLookupResult ToStudyLookupResult() =>
            StudyLookupResult.FromUpstream((HttpStatusCode)StatusCode, ContentType, Body);
    }
}
