using ClinicalTrials.Shared;

namespace ClinicalTrials.Mcp.Services;

public interface IStudyLookupEndpointClient
{
    Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken);
}
