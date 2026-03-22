namespace ClinicalTrials.Mcp.Runtime;

internal sealed class RequestBodyTooLargeException(long maxRequestBodySizeBytes)
    : Exception($"The request body exceeded the configured maximum size of {maxRequestBodySizeBytes} bytes.")
{
    public long MaxRequestBodySizeBytes { get; } = maxRequestBodySizeBytes;
}
