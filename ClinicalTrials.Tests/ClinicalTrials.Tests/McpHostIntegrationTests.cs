using System.Net;
using System.Text;
using ClinicalTrials.Mcp.Health;
using ClinicalTrials.Mcp.Tools;
using ClinicalTrials.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace ClinicalTrials.Tests;

public sealed class McpHostIntegrationTests
{
    [Fact]
    public async Task LiveHealthEndpoint_ReturnsOk()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"healthy\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ReadyHealthEndpoint_ReturnsOk()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadyHealthEndpoint_ReturnsServiceUnavailable_WhenClientRegistryDatabaseProbeFails()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(
            handler,
            configureTestServices: services =>
            {
                services.AddScoped<IClientRegistryDatabaseProbe, FailingClientRegistryDatabaseProbe>();
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ReadyHealthEndpoint_ReturnsServiceUnavailable_WhenRedisProbeFails()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        var configuration = new Dictionary<string, string?>
        {
            ["Caching:Provider"] = "Redis",
            ["ConnectionStrings:Redis"] = "localhost:6379"
        };

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(
            handler,
            configuration,
            configureTestServices: services =>
            {
                services.AddSingleton<IRedisDependencyProbe, FailingRedisDependencyProbe>();
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_RejectsUnauthenticatedRequests()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler);
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/mcp", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_ReturnsForbidden_ForDisabledClient()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ClientRegistry"] = "Server=integration;Database=ClinicalTrialsMcp;User Id=test;Password=test;"
        };
        var registryStore = TestClientRegistryStore.CreateDefault(TestWebApplicationFactory<StudiesMcpTools>.DefaultApiKey, clientIsActive: false);

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler, configuration, registryStore);
        using var client = factory.CreateAuthenticatedClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/mcp", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task McpServer_ListsStudyTool_AndInvokesIt()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608","briefTitle":"Study title"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler);
        using var httpClient = factory.CreateAuthenticatedClient();
        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                Name = "clinical-trials-test"
            },
            httpClient,
            NullLoggerFactory.Instance,
            ownsHttpClient: false);
        await using var mcpClient = await McpClient.CreateAsync(
            transport,
            clientOptions: null,
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: CancellationToken.None);

        var tools = await mcpClient.ListToolsAsync(cancellationToken: CancellationToken.None);
        var tool = Assert.Single(tools, t => t.Name == "get_study_by_nct_id");

        var result = await tool.CallAsync(
            arguments: new Dictionary<string, object?>
            {
                ["nctId"] = "NCT04924608"
            },
            progress: null,
            options: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(result.StructuredContent.HasValue);
        Assert.Equal("NCT04924608", result.StructuredContent.Value.GetProperty("nctId").GetString());
        Assert.Equal("Study title", result.StructuredContent.Value.GetProperty("briefTitle").GetString());
    }

    [Fact]
    public async Task McpEndpoint_ReturnsTooManyRequests_WhenPerClientRateLimitIsExceeded()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608","briefTitle":"Study title"}""")));

        var configuration = new Dictionary<string, string?>
        {
            ["RateLimiting:PerClientPermitLimit"] = "1",
            ["RateLimiting:GlobalPermitLimit"] = "10",
            ["RateLimiting:WindowSeconds"] = "60"
        };

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler, configuration);
        using var client = factory.CreateAuthenticatedClient();

        using var firstContent = new StringContent("{}", Encoding.UTF8, "application/json");
        using var secondContent = new StringContent("{}", Encoding.UTF8, "application/json");

        var firstResponse = await client.PostAsync("/mcp", firstContent);
        var secondResponse = await client.PostAsync("/mcp", secondContent);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_ReturnsPayloadTooLarge_WhenRequestBodyExceedsConfiguredLimit()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608","briefTitle":"Study title"}""")));

        var configuration = new Dictionary<string, string?>
        {
            ["RequestProtection:MaxRequestBodySizeBytes"] = "5"
        };

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler, configuration);
        using var client = factory.CreateAuthenticatedClient();
        using var content = new StringContent("""{"x":1}""", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/mcp", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private sealed class FailingClientRegistryDatabaseProbe : IClientRegistryDatabaseProbe
    {
        public Task ProbeAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("client registry down");
    }

    private sealed class FailingRedisDependencyProbe : IRedisDependencyProbe
    {
        public Task ProbeAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("redis down");
    }
}
