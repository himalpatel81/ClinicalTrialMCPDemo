namespace ClinicalTrials.Mcp.Observability;

public sealed class TelemetrySettings
{
    public const string SectionName = "Telemetry";

    public ConsoleTelemetrySettings Console { get; init; } = new();

    public ApplicationInsightsTelemetrySettings ApplicationInsights { get; init; } = new();
}

public sealed class ConsoleTelemetrySettings
{
    public bool Enabled { get; init; }
}

public sealed class ApplicationInsightsTelemetrySettings
{
    public string ConnectionString { get; init; } = string.Empty;
}
