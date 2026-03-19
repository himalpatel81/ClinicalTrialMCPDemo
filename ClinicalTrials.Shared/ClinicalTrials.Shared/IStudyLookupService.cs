namespace ClinicalTrials.Shared;

public interface IStudyLookupService
{
    Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken);
}
