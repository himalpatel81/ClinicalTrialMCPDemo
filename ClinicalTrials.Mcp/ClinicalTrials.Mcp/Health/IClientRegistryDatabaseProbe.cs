namespace ClinicalTrials.Mcp.Health;

public interface IClientRegistryDatabaseProbe
{
    Task ProbeAsync(CancellationToken cancellationToken);
}
