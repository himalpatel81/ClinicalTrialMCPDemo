using System.Net.Http;

namespace ClinicalTrials.Shared;

public sealed class StudyLookupService(IHttpClientFactory httpClientFactory) : IStudyLookupService
{
    public async Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClinicalTrialsServiceCollectionExtensions.ClinicalTrialsGovClientName);
        var upstreamPath = $"api/v2/studies/{Uri.EscapeDataString(nctId)}";

        try
        {
            using var upstreamResponse = await client.GetAsync(upstreamPath, cancellationToken);
            var body = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
            var contentType = upstreamResponse.Content.Headers.ContentType?.ToString();

            return StudyLookupResult.FromUpstream(upstreamResponse.StatusCode, contentType, body);
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
