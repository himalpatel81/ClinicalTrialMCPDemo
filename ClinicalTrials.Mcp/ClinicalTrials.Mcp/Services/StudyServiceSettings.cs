namespace ClinicalTrials.Mcp.Services;

public sealed class StudyServiceSettings
{
    public const string SectionName = "StudyService";
    public const string NctIdPlaceholder = "{nctId}";

    public string BaseUrl { get; init; } = string.Empty;

    public string StudyLookupPathTemplate { get; init; } = "api/studies/{nctId}";
}
