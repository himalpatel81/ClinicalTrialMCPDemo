using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;

namespace ClinicalTrials.Mcp.Runtime;

public sealed class McpExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<McpExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var (statusCode, title, detail, logLevel) = ResolveProblem(ex);
            logger.Log(
                logLevel,
                ex,
                "Unhandled exception while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var payload = JsonSerializer.Serialize(
                new
                {
                    title,
                    status = statusCode,
                    detail
                });

            await context.Response.WriteAsync(payload, context.RequestAborted);
        }
    }

    private static (int StatusCode, string Title, string Detail, LogLevel LogLevel) ResolveProblem(Exception exception) =>
        exception switch
        {
            RequestBodyTooLargeException requestTooLargeException => (
                StatusCodes.Status413PayloadTooLarge,
                "Payload Too Large",
                $"The request body exceeded the configured maximum size of {requestTooLargeException.MaxRequestBodySizeBytes} bytes.",
                LogLevel.Warning),
            BadHttpRequestException badHttpRequestException when badHttpRequestException.StatusCode == StatusCodes.Status413PayloadTooLarge => (
                StatusCodes.Status413PayloadTooLarge,
                "Payload Too Large",
                "The request body exceeded the configured maximum size.",
                LogLevel.Warning),
            BadHttpRequestException badHttpRequestException => (
                badHttpRequestException.StatusCode,
                "Bad Request",
                badHttpRequestException.Message,
                LogLevel.Warning),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "The MCP server encountered an unexpected error.",
                LogLevel.Error)
        };
}
