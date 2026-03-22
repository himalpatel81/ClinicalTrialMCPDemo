namespace ClinicalTrials.Mcp.Health;

public interface IRedisDependencyProbe
{
    Task ProbeAsync(CancellationToken cancellationToken);
}
