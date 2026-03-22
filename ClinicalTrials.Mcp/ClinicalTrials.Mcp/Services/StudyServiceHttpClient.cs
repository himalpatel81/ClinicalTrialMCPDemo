using System.Diagnostics;
using ClinicalTrials.Shared;
using ClinicalTrials.Mcp.Observability;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ClinicalTrials.Mcp.Services;

public class StudyServiceHttpClient(
    IHttpClientFactory httpClientFactory,
    McpTelemetry telemetry,
    IOptions<StudyServiceSettings> settings)
    : IStudyLookupEndpointClient
{
    public const string ClientName = "StudyService";

    private readonly StudyServiceSettings settings = settings.Value;

    public virtual async Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);
        var requestPath = this.settings.StudyLookupPathTemplate.Replace(
            StudyServiceSettings.NctIdPlaceholder,
            Uri.EscapeDataString(nctId),
            StringComparison.Ordinal);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(requestPath, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString();
            stopwatch.Stop();
            telemetry.RecordStudyServiceCall(response.StatusCode, StudyLookupFailureKind.None, stopwatch.Elapsed);

            return StudyLookupResult.FromUpstream(response.StatusCode, contentType, body);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            telemetry.RecordStudyServiceCall(statusCode: null, StudyLookupFailureKind.RequestFailed, stopwatch.Elapsed);
            return StudyLookupResult.RequestFailed(ex);
        }
        catch (BrokenCircuitException ex)
        {
            stopwatch.Stop();
            telemetry.RecordStudyServiceCall(statusCode: null, StudyLookupFailureKind.RequestFailed, stopwatch.Elapsed);
            return StudyLookupResult.RequestFailed(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            telemetry.RecordStudyServiceCall(statusCode: null, StudyLookupFailureKind.TimedOut, stopwatch.Elapsed);
            return StudyLookupResult.TimedOut(ex);
        }
        catch (TimeoutRejectedException ex)
        {
            stopwatch.Stop();
            telemetry.RecordStudyServiceCall(statusCode: null, StudyLookupFailureKind.TimedOut, stopwatch.Elapsed);
            return StudyLookupResult.TimedOut(ex);
        }
    }
}
