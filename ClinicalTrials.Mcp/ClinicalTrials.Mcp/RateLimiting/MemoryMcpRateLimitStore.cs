using System.Collections.Concurrent;

namespace ClinicalTrials.Mcp.RateLimiting;

public sealed class MemoryMcpRateLimitStore(TimeProvider timeProvider) : IMcpRateLimitStore
{
    private readonly ConcurrentDictionary<string, CounterState> counters = new();

    public Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        var expiresAt = timeProvider.GetUtcNow().Add(window);
        while (true)
        {
            var current = counters.GetOrAdd(key, _ => new CounterState(0, expiresAt));
            var next = current.ExpiresAt <= timeProvider.GetUtcNow()
                ? new CounterState(1, expiresAt)
                : current with { Count = current.Count + 1 };

            if (counters.TryUpdate(key, next, current))
            {
                return Task.FromResult(next.Count);
            }

            if (current.Count == 0 && counters.TryAdd(key, next))
            {
                return Task.FromResult(next.Count);
            }
        }
    }

    private sealed record CounterState(long Count, DateTimeOffset ExpiresAt);
}
