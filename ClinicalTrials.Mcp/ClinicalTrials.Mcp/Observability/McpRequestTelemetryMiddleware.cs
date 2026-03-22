using System.Diagnostics;
using System.Security.Claims;
using ClinicalTrials.Mcp.Authentication;

namespace ClinicalTrials.Mcp.Observability;

public sealed class McpRequestTelemetryMiddleware(
    RequestDelegate next,
    ILogger<McpRequestTelemetryMiddleware> logger,
    McpTelemetry telemetry)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var clientCode = context.User.FindFirstValue(ApiKeyAuthenticationDefaults.ClientIdClaimType);
            var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            var statusCode = context.Response.StatusCode;
            var correlationId = context.Items.TryGetValue(McpCorrelationMiddleware.CorrelationIdItemKey, out var value)
                ? value as string
                : context.TraceIdentifier;

            if (Activity.Current is not null)
            {
                Activity.Current.SetTag("mcp.correlation_id", correlationId);
                if (!string.IsNullOrWhiteSpace(clientCode))
                {
                    Activity.Current.SetTag("mcp.client_code", clientCode);
                    Activity.Current.SetTag("enduser.id", clientCode);
                }
            }

            telemetry.RecordRequest(
                context.Request.Method,
                path,
                statusCode,
                clientCode,
                stopwatch.Elapsed);

            LogRequest(path, statusCode, stopwatch.Elapsed, context.Request.Method, clientCode, correlationId);
        }
    }

    private void LogRequest(
        string path,
        int statusCode,
        TimeSpan elapsed,
        string method,
        string? clientCode,
        string? correlationId)
    {
        var logLevel = statusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Debug
                : LogLevel.Information;

        logger.Log(
            logLevel,
            "Completed {Method} {Path} with {StatusCode} in {ElapsedMilliseconds}ms for client {ClientCode} correlation {CorrelationId}",
            method,
            path,
            statusCode,
            Math.Round(elapsed.TotalMilliseconds, 2),
            string.IsNullOrWhiteSpace(clientCode) ? "anonymous" : clientCode,
            correlationId ?? string.Empty);
    }
}
