using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using ClinicalTrials.Shared;

namespace ClinicalTrials.Mcp.Observability;

public sealed class McpTelemetry : IDisposable
{
    public const string MeterName = "ClinicalTrials.Mcp";
    public const string ActivitySourceName = "ClinicalTrials.Mcp";

    private readonly ActivitySource activitySource = new(ActivitySourceName);
    private readonly Meter meter = new(MeterName);
    private readonly Counter<long> requestCounter;
    private readonly Histogram<double> requestDuration;
    private readonly Counter<long> authFailureCounter;
    private readonly Counter<long> rateLimitRejectionCounter;
    private readonly Counter<long> toolInvocationCounter;
    private readonly Histogram<double> toolDuration;
    private readonly Counter<long> studyServiceRequestCounter;
    private readonly Histogram<double> studyServiceDuration;
    private readonly Counter<long> cacheEventCounter;
    private readonly Counter<long> serverErrorCounter;

    public McpTelemetry()
    {
        requestCounter = meter.CreateCounter<long>("mcp.requests");
        requestDuration = meter.CreateHistogram<double>("mcp.request.duration", unit: "ms");
        authFailureCounter = meter.CreateCounter<long>("mcp.auth.failures");
        rateLimitRejectionCounter = meter.CreateCounter<long>("mcp.rate_limit.rejections");
        toolInvocationCounter = meter.CreateCounter<long>("mcp.tool.invocations");
        toolDuration = meter.CreateHistogram<double>("mcp.tool.duration", unit: "ms");
        studyServiceRequestCounter = meter.CreateCounter<long>("mcp.study_service.requests");
        studyServiceDuration = meter.CreateHistogram<double>("mcp.study_service.duration", unit: "ms");
        cacheEventCounter = meter.CreateCounter<long>("mcp.cache.events");
        serverErrorCounter = meter.CreateCounter<long>("mcp.server.errors");
    }

    public Activity? StartToolActivity(string toolName, string? clientCode)
    {
        var activity = activitySource.StartActivity($"mcp.tool.{toolName}", ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("mcp.tool.name", toolName);
        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            activity.SetTag("mcp.client_code", clientCode);
            activity.SetTag("enduser.id", clientCode);
        }

        return activity;
    }

    public void RecordRequest(string method, string route, int statusCode, string? clientCode, TimeSpan duration)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("http.method", method),
            new KeyValuePair<string, object?>("http.route", route),
            new KeyValuePair<string, object?>("http.status_code", statusCode)
        ];

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            tags.Add("mcp.client_code", clientCode);
        }

        requestCounter.Add(1, tags);
        requestDuration.Record(duration.TotalMilliseconds, tags);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            serverErrorCounter.Add(1, tags);
        }
    }

    public void RecordAuthFailure(string reason)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("reason", reason)
        ];

        authFailureCounter.Add(1, tags);
    }

    public void RecordRateLimitRejection(string scope, string? clientCode)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("scope", scope)
        ];

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            tags.Add("mcp.client_code", clientCode);
        }

        rateLimitRejectionCounter.Add(1, tags);
    }

    public void RecordToolInvocation(string toolName, string? clientCode, bool isSuccess, TimeSpan duration)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("tool", toolName),
            new KeyValuePair<string, object?>("outcome", isSuccess ? "success" : "error")
        ];

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            tags.Add("mcp.client_code", clientCode);
        }

        toolInvocationCounter.Add(1, tags);
        toolDuration.Record(duration.TotalMilliseconds, tags);
    }

    public void RecordStudyServiceCall(HttpStatusCode? statusCode, StudyLookupFailureKind failureKind, TimeSpan duration)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("failure_kind", failureKind.ToString())
        ];

        if (statusCode.HasValue)
        {
            tags.Add("http.status_code", (int)statusCode.Value);
        }

        studyServiceRequestCounter.Add(1, tags);
        studyServiceDuration.Record(duration.TotalMilliseconds, tags);
    }

    public void RecordCacheEvent(string result)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("result", result)
        ];

        cacheEventCounter.Add(1, tags);
    }

    public void Dispose()
    {
        activitySource.Dispose();
        meter.Dispose();
    }
}
