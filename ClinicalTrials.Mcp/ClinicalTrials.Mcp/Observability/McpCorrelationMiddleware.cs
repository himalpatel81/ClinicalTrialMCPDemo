using System.Diagnostics;

namespace ClinicalTrials.Mcp.Observability;

public sealed class McpCorrelationMiddleware(
    RequestDelegate next,
    ILogger<McpCorrelationMiddleware> logger)
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string CorrelationIdItemKey = "ClinicalTrials.Mcp.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        if (Activity.Current is not null)
        {
            Activity.Current.SetTag("mcp.correlation_id", correlationId);
        }

        using (logger.BeginScope(
                   new Dictionary<string, object?>
                   {
                       ["CorrelationId"] = correlationId
                   }))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var values))
        {
            var candidate = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
