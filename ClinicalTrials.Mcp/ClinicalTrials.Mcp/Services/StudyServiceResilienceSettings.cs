namespace ClinicalTrials.Mcp.Services;

public sealed class StudyServiceResilienceSettings
{
    public const string SectionName = "StudyService:Resilience";

    public int TotalRequestTimeoutSeconds { get; init; } = 15;

    public int AttemptTimeoutSeconds { get; init; } = 5;

    public int MaxRetryAttempts { get; init; } = 2;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public int CircuitBreakerMinimumThroughput { get; init; } = 5;

    public int CircuitBreakerSamplingWindowSeconds { get; init; } = 30;

    public int CircuitBreakerBreakDurationSeconds { get; init; } = 30;
}
