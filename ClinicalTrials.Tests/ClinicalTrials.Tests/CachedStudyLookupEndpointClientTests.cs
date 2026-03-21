using System.Net;
using ClinicalTrials.Mcp.Caching;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Shared;
using ClinicalTrials.Tests.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Tests;

public sealed class CachedStudyLookupEndpointClientTests
{
    [Fact]
    public async Task GetStudyAsync_CachesSuccessfulResponses()
    {
        var innerClient = new StubStudyLookupEndpointClient(
            StudyLookupResult.FromUpstream(HttpStatusCode.OK, "application/json", """{"nctId":"NCT04924608"}"""));
        var cacheStore = new MemoryStudyLookupCacheStore(new MemoryCache(new MemoryCacheOptions()));
        var client = CreateClient(innerClient, cacheStore);

        var first = await client.GetStudyAsync("NCT04924608", CancellationToken.None);
        var second = await client.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, innerClient.CallCount);
    }

    [Fact]
    public async Task GetStudyAsync_CachesNotFoundResponses_WhenNegativeCachingEnabled()
    {
        var innerClient = new StubStudyLookupEndpointClient(
            StudyLookupResult.FromUpstream(HttpStatusCode.NotFound, "application/json", """{"message":"not found"}"""));
        var cacheStore = new MemoryStudyLookupCacheStore(new MemoryCache(new MemoryCacheOptions()));
        var client = CreateClient(innerClient, cacheStore);

        await client.GetStudyAsync("NCT404", CancellationToken.None);
        await client.GetStudyAsync("NCT404", CancellationToken.None);

        Assert.Equal(1, innerClient.CallCount);
    }

    [Fact]
    public async Task GetStudyAsync_DoesNotCacheFailures()
    {
        var innerClient = new StubStudyLookupEndpointClient(
            StudyLookupResult.RequestFailed(new HttpRequestException("boom")));
        var cacheStore = new MemoryStudyLookupCacheStore(new MemoryCache(new MemoryCacheOptions()));
        var client = CreateClient(innerClient, cacheStore);

        await client.GetStudyAsync("NCT04924608", CancellationToken.None);
        await client.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.Equal(2, innerClient.CallCount);
    }

    private static CachedStudyLookupEndpointClient CreateClient(
        StubStudyLookupEndpointClient innerClient,
        IStudyLookupCacheStore cacheStore) =>
        new(
            new StudyServiceHttpClientAdapter(innerClient),
            cacheStore,
            Options.Create(
                new StudyLookupCacheSettings
                {
                    Provider = CacheProvider.Memory,
                    SuccessTtlSeconds = 300,
                    NotFoundTtlSeconds = 60
                }));

    private sealed class StubStudyLookupEndpointClient(StudyLookupResult result) : IStudyLookupEndpointClient
    {
        public int CallCount { get; private set; }

        public Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class StudyServiceHttpClientAdapter(IStudyLookupEndpointClient innerClient)
        : StudyServiceHttpClient(new TestHttpClientFactory(new HttpClient()), Options.Create(new StudyServiceSettings()))
    {
        public override Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken) =>
            innerClient.GetStudyAsync(nctId, cancellationToken);
    }
}
