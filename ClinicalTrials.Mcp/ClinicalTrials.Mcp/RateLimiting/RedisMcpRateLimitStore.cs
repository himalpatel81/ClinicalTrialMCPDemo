using StackExchange.Redis;

namespace ClinicalTrials.Mcp.RateLimiting;

public sealed class RedisMcpRateLimitStore(IConnectionMultiplexer connectionMultiplexer) : IMcpRateLimitStore
{
    private const string IncrementScript =
        """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    public async Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        var database = connectionMultiplexer.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
            IncrementScript,
            [key],
            [Math.Max(1, (int)Math.Ceiling(window.TotalSeconds))]);

        return (long)result;
    }
}
