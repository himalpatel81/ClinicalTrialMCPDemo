using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using ClinicalTrials.Mcp.Authentication;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.RateLimiting;

public sealed class McpRateLimitingMiddleware(
    RequestDelegate next,
    IMcpRateLimitStore rateLimitStore,
    IOptions<McpRateLimitingSettings> settings,
    TimeProvider timeProvider)
{
    private readonly McpRateLimitingSettings settings = settings.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var window = TimeSpan.FromSeconds(this.settings.WindowSeconds);
        var retryAfterSeconds = Math.Max(1, this.settings.WindowSeconds);
        var windowBucket = timeProvider.GetUtcNow().ToUnixTimeSeconds() / this.settings.WindowSeconds;

        var globalCount = await rateLimitStore.IncrementAsync(
            $"mcp:ratelimit:global:{windowBucket}",
            window,
            context.RequestAborted);

        if (globalCount > this.settings.GlobalPermitLimit)
        {
            await WriteTooManyRequestsAsync(context, retryAfterSeconds, "Global MCP rate limit exceeded.");
            return;
        }

        var clientId = context.User.FindFirstValue(ApiKeyAuthenticationDefaults.ClientIdClaimType);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            await next(context);
            return;
        }

        var permitLimit = ResolveClientPermitLimit(context.User);
        var clientCount = await rateLimitStore.IncrementAsync(
            $"mcp:ratelimit:client:{clientId}:{windowBucket}",
            window,
            context.RequestAborted);

        if (clientCount > permitLimit)
        {
            await WriteTooManyRequestsAsync(context, retryAfterSeconds, "Client MCP rate limit exceeded.");
            return;
        }

        await next(context);
    }

    private int ResolveClientPermitLimit(ClaimsPrincipal user)
    {
        var claimValue = user.FindFirstValue(ApiKeyAuthenticationDefaults.PermitLimitClaimType);
        if (int.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var permitLimit) &&
            permitLimit > 0)
        {
            return permitLimit;
        }

        return settings.PerClientPermitLimit;
    }

    private static async Task WriteTooManyRequestsAsync(HttpContext context, int retryAfterSeconds, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var payload = JsonSerializer.Serialize(
            new
            {
                title = "Too Many Requests",
                status = StatusCodes.Status429TooManyRequests,
                detail
            });

        await context.Response.WriteAsync(payload, context.RequestAborted);
    }
}
