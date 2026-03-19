using System.Net;
using System.Net.Http.Json;
using ClinicalTrials.Api.Controllers;
using ClinicalTrials.Tests.Testing;

namespace ClinicalTrials.Tests;

public sealed class StudiesControllerIntegrationTests
{
    [Fact]
    public async Task GetStudyAsync_PassesThroughSuccessfulResponse()
    {
        var handler = new TestHttpMessageHandler((request, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesController>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/studies/NCT04924608");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"nctId":"NCT04924608"}""", await response.Content.ReadAsStringAsync());
        Assert.Single(handler.Requests);
        Assert.EndsWith("/api/v2/studies/NCT04924608", handler.Requests[0].RequestUri?.ToString());
    }

    [Fact]
    public async Task GetStudyAsync_PassesThroughUpstreamNotFound()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.NotFound,
                    """{"message":"not found"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesController>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/studies/NCT404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("""{"message":"not found"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetStudyAsync_ReturnsBadRequestForWhitespaceRouteValue()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(TestHttpMessageHandler.JsonResponse(HttpStatusCode.OK, """{"unused":true}""")));

        await using var factory = new TestWebApplicationFactory<StudiesController>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/studies/%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.NotNull(problem.Title);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetStudyAsync_ReturnsBadGatewayForUpstreamConnectivityFailure()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new HttpRequestException("boom"));

        await using var factory = new TestWebApplicationFactory<StudiesController>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/studies/NCT04924608");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("ClinicalTrials.gov request failed", problem.Title);
    }

    [Fact]
    public async Task GetStudyAsync_ReturnsGatewayTimeoutForUpstreamTimeout()
    {
        var handler = new TestHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));

        await using var factory = new TestWebApplicationFactory<StudiesController>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/studies/NCT04924608");

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("ClinicalTrials.gov request timed out", problem.Title);
    }

    private sealed record ProblemDetailsDto(string? Title, string? Detail, int? Status);
}
