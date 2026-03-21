using System.Net;
using System.Text;
using ClinicalTrials.Mcp.Tools;
using ClinicalTrials.Tests.Testing;
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
            ["Authentication:ApiKeys:Clients:0:Enabled"] = "false"
        };

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler, configuration);
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
}
