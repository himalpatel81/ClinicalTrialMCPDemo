using ClinicalTrials.Shared;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Services;

public sealed class StudyServiceHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<StudyServiceSettings> settings)
    : IStudyLookupEndpointClient
{
    public const string ClientName = "StudyService";

    private readonly StudyServiceSettings settings = settings.Value;

    public async Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);
        var requestPath = this.settings.StudyLookupPathTemplate.Replace(
            StudyServiceSettings.NctIdPlaceholder,
            Uri.EscapeDataString(nctId),
            StringComparison.Ordinal);

        try
        {
            using var response = await client.GetAsync(requestPath, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString();

            return StudyLookupResult.FromUpstream(response.StatusCode, contentType, body);
        }
        catch (HttpRequestException ex)
        {
            return StudyLookupResult.RequestFailed(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return StudyLookupResult.TimedOut(ex);
        }
    }
}
