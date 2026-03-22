using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Runtime;

public sealed class McpRequestProtectionMiddleware(
    RequestDelegate next,
    IOptions<McpRequestProtectionSettings> settings)
{
    private readonly McpRequestProtectionSettings settings = settings.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!CanHaveRequestBody(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (context.Request.ContentLength.HasValue &&
            context.Request.ContentLength.Value > settings.MaxRequestBodySizeBytes)
        {
            await WritePayloadTooLargeAsync(context, settings.MaxRequestBodySizeBytes);
            return;
        }

        var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature is not null && !maxRequestBodySizeFeature.IsReadOnly)
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = settings.MaxRequestBodySizeBytes;
        }

        var originalBody = context.Request.Body;
        context.Request.Body = new RequestBodySizeLimitedStream(originalBody, settings.MaxRequestBodySizeBytes);

        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Body = originalBody;
        }
    }

    private static bool CanHaveRequestBody(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method);

    private static async Task WritePayloadTooLargeAsync(HttpContext context, long maxRequestBodySizeBytes)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/problem+json";

        var payload = JsonSerializer.Serialize(
            new
            {
                title = "Payload Too Large",
                status = StatusCodes.Status413PayloadTooLarge,
                detail = $"The request body exceeded the configured maximum size of {maxRequestBodySizeBytes.ToString(CultureInfo.InvariantCulture)} bytes."
            });

        await context.Response.WriteAsync(payload, context.RequestAborted);
    }
}
