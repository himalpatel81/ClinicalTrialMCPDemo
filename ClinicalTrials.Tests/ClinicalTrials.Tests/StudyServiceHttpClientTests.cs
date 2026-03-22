using System.Net;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Shared;
using ClinicalTrials.Tests.Testing;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ClinicalTrials.Tests;

public sealed class StudyServiceHttpClientTests
{
    [Fact]
    public async Task GetStudyAsync_CallsConfiguredStudyEndpoint()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        var service = CreateService(
            handler,
            new StudyServiceSettings
            {
                BaseUrl = "https://clinicaltrials.api.test/",
                StudyLookupPathTemplate = "api/studies/{nctId}"
            });

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.False(result.HasFailure);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Single(handler.Requests);
        Assert.Equal("https://clinicaltrials.api.test/api/studies/NCT04924608", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetStudyAsync_UsesConfigurablePathTemplate()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        var service = CreateService(
            handler,
            new StudyServiceSettings
            {
                BaseUrl = "https://future-study-service.test/",
                StudyLookupPathTemplate = "vNext/studies/by-nct/{nctId}"
            });

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.False(result.HasFailure);
        Assert.Single(handler.Requests);
        Assert.Equal("https://future-study-service.test/vNext/studies/by-nct/NCT04924608", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetStudyAsync_MapsHttpRequestExceptionToRequestFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new HttpRequestException("boom"));
        var service = CreateService(handler, DefaultSettings);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.HasFailure);
        Assert.Equal(StudyLookupFailureKind.RequestFailed, result.FailureKind);
    }

    [Fact]
    public async Task GetStudyAsync_MapsTimeoutToTimedOutFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));
        var service = CreateService(handler, DefaultSettings);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.HasFailure);
        Assert.Equal(StudyLookupFailureKind.TimedOut, result.FailureKind);
    }

    [Fact]
    public async Task GetStudyAsync_MapsTimeoutRejectedExceptionToTimedOutFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new TimeoutRejectedException("timeout"));
        var service = CreateService(handler, DefaultSettings);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.HasFailure);
        Assert.Equal(StudyLookupFailureKind.TimedOut, result.FailureKind);
    }

    [Fact]
    public async Task GetStudyAsync_MapsBrokenCircuitExceptionToRequestFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new BrokenCircuitException("circuit open"));
        var service = CreateService(handler, DefaultSettings);

        var result = await service.GetStudyAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.HasFailure);
        Assert.Equal(StudyLookupFailureKind.RequestFailed, result.FailureKind);
    }

    private static StudyServiceSettings DefaultSettings => new()
    {
        BaseUrl = "https://clinicaltrials.api.test/",
        StudyLookupPathTemplate = "api/studies/{nctId}"
    };

    private static StudyServiceHttpClient CreateService(HttpMessageHandler handler, StudyServiceSettings settings)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.BaseUrl)
        };

        return new StudyServiceHttpClient(new TestHttpClientFactory(httpClient), Options.Create(settings));
    }
}
