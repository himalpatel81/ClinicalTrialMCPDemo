using System.Net;
using ClinicalTrials.Mcp.Tools;
using ClinicalTrials.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace ClinicalTrials.Tests;

public sealed class StudiesMcpToolsTests
{
    [Fact]
    public async Task GetStudyByNctIdAsync_ReturnsStructuredJsonForSuccessfulLookup()
    {
        var service = new StubStudyLookupService
        {
            Result = StudyLookupResult.FromUpstream(
                HttpStatusCode.OK,
                "application/json",
                """{"nctId":"NCT04924608","briefTitle":"Study title"}""")
        };

        var tool = new StudiesMcpTools(service, NullLogger<StudiesMcpTools>.Instance);

        var result = await tool.GetStudyByNctIdAsync(" NCT04924608 ", CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("NCT04924608", service.LastNctId);
        Assert.True(result.StructuredContent.HasValue);
        var structuredContent = result.StructuredContent.Value;
        Assert.Equal("NCT04924608", structuredContent.GetProperty("nctId").GetString());
        Assert.Equal("Study title", structuredContent.GetProperty("briefTitle").GetString());

        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("NCT04924608", textBlock.Text);
    }

    [Fact]
    public async Task GetStudyByNctIdAsync_ReturnsValidationErrorForBlankInput()
    {
        var service = new StubStudyLookupService();
        var tool = new StudiesMcpTools(service, NullLogger<StudiesMcpTools>.Instance);

        var result = await tool.GetStudyByNctIdAsync("   ", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Null(service.LastNctId);
        Assert.True(result.StructuredContent.HasValue);
        var structuredContent = result.StructuredContent.Value;
        Assert.Equal(400, structuredContent.GetProperty("statusCode").GetInt32());
        Assert.Equal("Invalid NCT ID", structuredContent.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetStudyByNctIdAsync_ReturnsStructuredErrorForUpstreamNotFound()
    {
        var service = new StubStudyLookupService
        {
            Result = StudyLookupResult.FromUpstream(
                HttpStatusCode.NotFound,
                "application/json",
                """{"message":"not found"}""")
        };

        var tool = new StudiesMcpTools(service, NullLogger<StudiesMcpTools>.Instance);

        var result = await tool.GetStudyByNctIdAsync("NCT404", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.True(result.StructuredContent.HasValue);
        var structuredContent = result.StructuredContent.Value;
        Assert.Equal(404, structuredContent.GetProperty("statusCode").GetInt32());
        Assert.Equal("ClinicalTrials.gov returned an error", structuredContent.GetProperty("title").GetString());
        Assert.Equal("not found", structuredContent.GetProperty("upstreamBody").GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetStudyByNctIdAsync_ReturnsStructuredErrorForConnectivityFailure()
    {
        var service = new StubStudyLookupService
        {
            Result = StudyLookupResult.RequestFailed(new HttpRequestException("boom"))
        };

        var tool = new StudiesMcpTools(service, NullLogger<StudiesMcpTools>.Instance);

        var result = await tool.GetStudyByNctIdAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.True(result.StructuredContent.HasValue);
        var structuredContent = result.StructuredContent.Value;
        Assert.Equal(502, structuredContent.GetProperty("statusCode").GetInt32());
        Assert.Equal("ClinicalTrials.gov request failed", structuredContent.GetProperty("title").GetString());
    }

    [Fact]
    public async Task GetStudyByNctIdAsync_ReturnsStructuredErrorForTimeout()
    {
        var service = new StubStudyLookupService
        {
            Result = StudyLookupResult.TimedOut(new TaskCanceledException("timeout"))
        };

        var tool = new StudiesMcpTools(service, NullLogger<StudiesMcpTools>.Instance);

        var result = await tool.GetStudyByNctIdAsync("NCT04924608", CancellationToken.None);

        Assert.True(result.IsError);
        Assert.True(result.StructuredContent.HasValue);
        var structuredContent = result.StructuredContent.Value;
        Assert.Equal(504, structuredContent.GetProperty("statusCode").GetInt32());
        Assert.Equal("ClinicalTrials.gov request timed out", structuredContent.GetProperty("title").GetString());
    }

    private sealed class StubStudyLookupService : IStudyLookupService
    {
        public string? LastNctId { get; private set; }

        public StudyLookupResult Result { get; set; } =
            StudyLookupResult.FromUpstream(HttpStatusCode.OK, "application/json", "{}");

        public Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
        {
            LastNctId = nctId;
            return Task.FromResult(Result);
        }
    }
}
