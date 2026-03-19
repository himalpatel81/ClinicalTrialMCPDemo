using System.Net;
using ClinicalTrials.Shared;
using ClinicalTrials.Tests.Testing;

namespace ClinicalTrials.Tests;

public sealed class StudyLookupServiceTests
{
    [Fact]
    public async Task GetStudyAsync_PassesThroughSuccessfulUpstreamResponse()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        var service = CreateService(handler);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.False(result.HasFailure);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("""{"nctId":"NCT04924608"}""", result.Body);
        Assert.Single(handler.Requests);
        Assert.Equal("https://clinicaltrials.test/api/v2/studies/NCT04924608", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetStudyAsync_PassesThroughNonSuccessUpstreamResponse()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.NotFound,
                    """{"message":"not found"}""")));

        var service = CreateService(handler);

        var result = await service.GetStudyAsync("NCT404", CancellationToken.None);

        Assert.False(result.HasFailure);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("""{"message":"not found"}""", result.Body);
        Assert.Single(handler.Requests);
        Assert.Equal("https://clinicaltrials.test/api/v2/studies/NCT404", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetStudyAsync_MapsHttpRequestExceptionToRequestFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new HttpRequestException("boom"));
        var service = CreateService(handler);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.HasFailure);
        Assert.Equal(StudyLookupFailureKind.RequestFailed, result.FailureKind);
        Assert.IsType<HttpRequestException>(result.Exception);
    }

    [Fact]
    public async Task GetStudyAsync_MapsTimeoutToTimedOutFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));
        var service = CreateService(handler);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.HasFailure);
        Assert.Equal(StudyLookupFailureKind.TimedOut, result.FailureKind);
        Assert.IsType<TaskCanceledException>(result.Exception);
    }

    private static StudyLookupService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://clinicaltrials.test/")
        };

        return new StudyLookupService(new TestHttpClientFactory(httpClient));
    }
}
