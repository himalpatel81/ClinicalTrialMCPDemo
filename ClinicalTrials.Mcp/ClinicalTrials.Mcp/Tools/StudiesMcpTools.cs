using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Claims;
using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Observability;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Shared;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ClinicalTrials.Mcp.Tools;

[McpServerToolType]
public sealed class StudiesMcpTools(
    IStudyLookupEndpointClient studyLookupClient,
    IHttpContextAccessor httpContextAccessor,
    McpTelemetry telemetry,
    ILogger<StudiesMcpTools> logger)
{
    private const string ToolName = "get_study_by_nct_id";

    [McpServerTool(
        Name = ToolName,
        Title = "Get Study By NCT ID",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Fetch a study record by its NCT identifier, for example NCT04924608.")]
    public async Task<CallToolResult> GetStudyByNctIdAsync(
        [Description("The NCT identifier to look up, for example NCT04924608.")]
        string nctId,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var clientCode = httpContextAccessor.HttpContext?.User.FindFirstValue(ApiKeyAuthenticationDefaults.ClientIdClaimType);
        using var activity = telemetry.StartToolActivity(ToolName, clientCode);

        var trimmedNctId = nctId.Trim();

        if (string.IsNullOrWhiteSpace(trimmedNctId))
        {
            var error = CreateErrorResult(
                HttpStatusCode.BadRequest,
                "Invalid NCT ID",
                "The nctId parameter is required.");
            telemetry.RecordToolInvocation(ToolName, clientCode, isSuccess: false, stopwatch.Elapsed);
            return error;
        }

        var result = await studyLookupClient.GetStudyAsync(trimmedNctId, cancellationToken);

        if (result.FailureKind == StudyLookupFailureKind.RequestFailed)
        {
            logger.LogError(result.Exception, "Failed to reach the configured study service for NCT ID {NctId}", trimmedNctId);

            var error = CreateErrorResult(
                HttpStatusCode.BadGateway,
                "Study service request failed",
                "The configured study service could not be reached.");
            telemetry.RecordToolInvocation(ToolName, clientCode, isSuccess: false, stopwatch.Elapsed);
            return error;
        }

        if (result.FailureKind == StudyLookupFailureKind.TimedOut)
        {
            logger.LogWarning(result.Exception, "The configured study service timed out for NCT ID {NctId}", trimmedNctId);

            var error = CreateErrorResult(
                HttpStatusCode.GatewayTimeout,
                "Study service request timed out",
                "The configured study service did not respond in time.");
            telemetry.RecordToolInvocation(ToolName, clientCode, isSuccess: false, stopwatch.Elapsed);
            return error;
        }

        if (result.StatusCode is null)
        {
            var error = CreateErrorResult(
                HttpStatusCode.InternalServerError,
                "Study lookup failed",
                "The study lookup service returned no status code.");
            telemetry.RecordToolInvocation(ToolName, clientCode, isSuccess: false, stopwatch.Elapsed);
            return error;
        }

        if (!IsSuccessStatusCode(result.StatusCode.Value))
        {
            var detail = $"The configured study service returned {(int)result.StatusCode.Value} {result.StatusCode.Value} for NCT ID {trimmedNctId}.";

            var error = CreateErrorResult(
                result.StatusCode.Value,
                "Study service returned an error",
                detail,
                result.Body);
            telemetry.RecordToolInvocation(ToolName, clientCode, isSuccess: false, stopwatch.Elapsed);
            return error;
        }

        var structuredContent = ParseStudyPayload(result.Body);
        telemetry.RecordToolInvocation(ToolName, clientCode, isSuccess: true, stopwatch.Elapsed);

        return new CallToolResult
        {
            IsError = false,
            Content =
            [
                new TextContentBlock
                {
                    Text = BuildStudySummary(structuredContent, trimmedNctId)
                }
            ],
            StructuredContent = structuredContent
        };
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode) =>
        (int)statusCode >= 200 && (int)statusCode <= 299;

    private static JsonElement ParseStudyPayload(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static string BuildStudySummary(JsonElement structuredContent, string requestedNctId)
    {
        var nctId = TryGetString(
            structuredContent,
            ["protocolSection", "identificationModule", "nctId"])
            ?? TryGetString(structuredContent, ["nctId"])
            ?? requestedNctId;

        var title = TryGetString(
            structuredContent,
            ["protocolSection", "identificationModule", "briefTitle"])
            ?? TryGetString(
                structuredContent,
                ["protocolSection", "identificationModule", "officialTitle"])
            ?? TryGetString(structuredContent, ["briefTitle"])
            ?? TryGetString(structuredContent, ["officialTitle"]);

        var overallStatus = TryGetString(
            structuredContent,
            ["protocolSection", "statusModule", "overallStatus"])
            ?? TryGetString(structuredContent, ["overallStatus"]);

        var studyType = TryGetString(
            structuredContent,
            ["protocolSection", "designModule", "studyType"])
            ?? TryGetString(structuredContent, ["studyType"]);

        var summary = new StringBuilder();
        summary.AppendLine("Study retrieved successfully.");
        summary.AppendLine($"NCT ID: {nctId}");

        if (!string.IsNullOrWhiteSpace(title))
        {
            summary.AppendLine($"Title: {title}");
        }

        if (!string.IsNullOrWhiteSpace(overallStatus))
        {
            summary.AppendLine($"Overall status: {overallStatus}");
        }

        if (!string.IsNullOrWhiteSpace(studyType))
        {
            summary.AppendLine($"Study type: {studyType}");
        }

        return summary.ToString().TrimEnd();
    }

    private static CallToolResult CreateErrorResult(
        HttpStatusCode statusCode,
        string title,
        string detail,
        string? upstreamBody = null)
    {
        var structuredContent = BuildErrorStructuredContent(statusCode, title, detail, upstreamBody);

        return new CallToolResult
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = $"{title}: {detail}"
                }
            ],
            StructuredContent = structuredContent
        };
    }

    private static JsonElement BuildErrorStructuredContent(
        HttpStatusCode statusCode,
        string title,
        string detail,
        string? upstreamBody)
    {
        using var bodyDocument = TryParseJsonDocument(upstreamBody);

        var payload = new Dictionary<string, object?>
        {
            ["statusCode"] = (int)statusCode,
            ["title"] = title,
            ["detail"] = detail
        };

        if (bodyDocument is not null)
        {
            payload["upstreamBody"] = bodyDocument.RootElement.Clone();
        }
        else if (!string.IsNullOrWhiteSpace(upstreamBody))
        {
            payload["upstreamBody"] = upstreamBody;
        }

        return JsonSerializer.SerializeToElement(payload);
    }

    private static JsonDocument? TryParseJsonDocument(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string[] path)
    {
        var current = element;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }
}
