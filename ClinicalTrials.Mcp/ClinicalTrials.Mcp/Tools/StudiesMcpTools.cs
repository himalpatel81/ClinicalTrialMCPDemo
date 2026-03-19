using System.ComponentModel;
using System.Net;
using System.Text.Json;
using ClinicalTrials.Shared;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ClinicalTrials.Mcp.Tools;

[McpServerToolType]
public sealed class StudiesMcpTools(IStudyLookupService studyLookupService, ILogger<StudiesMcpTools> logger)
{
    [McpServerTool(
        Name = "get_study_by_nct_id",
        Title = "Get Study By NCT ID",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Fetch a ClinicalTrials.gov study record by its NCT identifier, for example NCT04924608.")]
    public async Task<CallToolResult> GetStudyByNctIdAsync(
        [Description("The ClinicalTrials.gov NCT identifier to look up, for example NCT04924608.")]
        string nctId,
        CancellationToken cancellationToken)
    {
        var trimmedNctId = nctId.Trim();

        if (string.IsNullOrWhiteSpace(trimmedNctId))
        {
            return CreateErrorResult(
                HttpStatusCode.BadRequest,
                "Invalid NCT ID",
                "The nctId parameter is required.");
        }

        var result = await studyLookupService.GetStudyAsync(trimmedNctId, cancellationToken);

        if (result.FailureKind == StudyLookupFailureKind.RequestFailed)
        {
            logger.LogError(result.Exception, "Failed to reach ClinicalTrials.gov for NCT ID {NctId}", trimmedNctId);

            return CreateErrorResult(
                HttpStatusCode.BadGateway,
                "ClinicalTrials.gov request failed",
                "The upstream ClinicalTrials.gov service could not be reached.");
        }

        if (result.FailureKind == StudyLookupFailureKind.TimedOut)
        {
            logger.LogWarning(result.Exception, "ClinicalTrials.gov timed out for NCT ID {NctId}", trimmedNctId);

            return CreateErrorResult(
                HttpStatusCode.GatewayTimeout,
                "ClinicalTrials.gov request timed out",
                "The upstream ClinicalTrials.gov service did not respond in time.");
        }

        if (result.StatusCode is null)
        {
            return CreateErrorResult(
                HttpStatusCode.InternalServerError,
                "Study lookup failed",
                "The study lookup service returned no status code.");
        }

        if (!IsSuccessStatusCode(result.StatusCode.Value))
        {
            var detail = $"ClinicalTrials.gov returned {(int)result.StatusCode.Value} {result.StatusCode.Value} for NCT ID {trimmedNctId}.";

            return CreateErrorResult(
                result.StatusCode.Value,
                "ClinicalTrials.gov returned an error",
                detail,
                result.Body);
        }

        var structuredContent = ParseStudyPayload(result.Body);

        return new CallToolResult
        {
            IsError = false,
            Content =
            [
                new TextContentBlock
                {
                    Text = $"Study {trimmedNctId} retrieved successfully."
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
}
