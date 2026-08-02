namespace MagicBox.Core;

public sealed class InMemoryProxyPool : IProxyPool
{
    private readonly Dictionary<string, PoolState> pools;
    private readonly object sync = new();

    public InMemoryProxyPool(IReadOnlyDictionary<string, IReadOnlyList<Uri>> endpoints)
    {
        pools = endpoints.ToDictionary(
            pair => pair.Key,
            pair => new PoolState(pair.Value.Where(IsSupported).Distinct().ToArray()),
            StringComparer.OrdinalIgnoreCase);
    }

    public Uri? Next(string poolName)
    {
        lock (sync)
        {
            if (!pools.TryGetValue(poolName, out var pool) || pool.Endpoints.Count == 0)
            {
                return null;
            }

            for (var attempt = 0; attempt < pool.Endpoints.Count; attempt++)
            {
                var endpoint = pool.Endpoints[pool.NextIndex++ % pool.Endpoints.Count];
                if (!pool.QuarantinedUntil.TryGetValue(endpoint, out var until) || until <= DateTimeOffset.UtcNow)
                {
                    return endpoint;
                }
            }

            return null;
        }
    }

    public void Report(string poolName, Uri proxyUri, bool successful)
    {
        lock (sync)
        {
            if (!pools.TryGetValue(poolName, out var pool))
            {
                return;
            }

            if (successful)
            {
                pool.Failures.Remove(proxyUri);
                pool.QuarantinedUntil.Remove(proxyUri);
                return;
            }

            var failures = pool.Failures.TryGetValue(proxyUri, out var count) ? count + 1 : 1;
            pool.Failures[proxyUri] = failures;
            if (failures >= 3)
            {
                pool.QuarantinedUntil[proxyUri] = DateTimeOffset.UtcNow.AddMinutes(5);
                pool.Failures[proxyUri] = 0;
            }
        }
    }

    private static bool IsSupported(Uri uri) => uri.Scheme is "http" or "https" or "socks5";

    private sealed class PoolState
    {
        public IReadOnlyList<Uri> Endpoints { get; }
        public Dictionary<Uri, int> Failures { get; } = new();
        public Dictionary<Uri, DateTimeOffset> QuarantinedUntil { get; } = new();
        public int NextIndex { get; set; }

        public PoolState(IReadOnlyList<Uri> endpoints) => Endpoints = endpoints;
    }
}
