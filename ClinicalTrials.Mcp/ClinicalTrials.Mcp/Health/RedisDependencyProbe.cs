using StackExchange.Redis;

namespace ClinicalTrials.Mcp.Health;

internal sealed class RedisDependencyProbe(IConnectionMultiplexer connectionMultiplexer) : IRedisDependencyProbe
{
    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = connectionMultiplexer.GetDatabase();
        _ = await database.PingAsync();
    }
}
