using System.Net;
using ClinicalTrials.Mcp.Tools;
using ClinicalTrials.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ClinicalTrials.Tests;

public sealed class McpHostIntegrationTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(
                TestHttpMessageHandler.JsonResponse(
                    HttpStatusCode.OK,
                    """{"nctId":"NCT04924608"}""")));

        await using var factory = new TestWebApplicationFactory<StudiesMcpTools>(handler);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"healthy\"", await response.Content.ReadAsStringAsync());
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
        using var httpClient = factory.CreateClient();
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
