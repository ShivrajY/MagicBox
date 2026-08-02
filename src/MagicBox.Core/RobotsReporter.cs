using System.Net;

namespace MagicBox.Core;

public sealed class RobotsReporter : IRobotsReporter
{
    private readonly HttpClient client = new();
    private readonly Dictionary<string, RobotsResult> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object cacheLock = new();

    public async Task<RobotsResult> ReportAsync(Uri target, string userAgent, CancellationToken cancellationToken)
    {
        var key = $"{target.Scheme}://{target.Host}:{target.Port}";
        lock (cacheLock)
        {
            if (cache.TryGetValue(key, out var cached))
            {
                return cached with { PathAllowed = IsAllowed(cached.Rules, target.AbsolutePath, userAgent) };
            }
        }

        var robotsUri = new Uri(new Uri(key), "/robots.txt");
        string body;
        try
        {
            using var response = await client.GetAsync(robotsUri, cancellationToken).ConfigureAwait(false);
            body = response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : string.Empty;
        }
        catch (HttpRequestException)
        {
            body = string.Empty;
        }

        var result = new RobotsResult
        {
            RobotsUri = robotsUri,
            Retrieved = true,
            Rules = Parse(body),
            PathAllowed = true
        };
        lock (cacheLock)
        {
            cache[key] = result;
        }

        return result with { PathAllowed = IsAllowed(result.Rules, target.AbsolutePath, userAgent) };
    }

    private static IReadOnlyList<RobotsRule> Parse(string body)
    {
        var rules = new List<RobotsRule>();
        string? agent = null;
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (name.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                agent = value;
            }
            else if (agent is not null && name.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(new RobotsRule(agent, value));
            }
        }

        return rules;
    }

    private static bool IsAllowed(IReadOnlyList<RobotsRule> rules, string path, string userAgent)
    {
        var matching = rules.Where(rule => rule.Agent == "*" || userAgent.Contains(rule.Agent, StringComparison.OrdinalIgnoreCase));
        var longest = matching.Where(rule => !string.IsNullOrEmpty(rule.Path) && path.StartsWith(rule.Path, StringComparison.Ordinal)).OrderByDescending(rule => rule.Path.Length).FirstOrDefault();
        return longest is null;
    }
}

public sealed record RobotsRule(string Agent, string Path);

public sealed record RobotsResult
{
    public Uri RobotsUri { get; init; } = new("https://invalid.local/robots.txt");
    public bool Retrieved { get; init; }
    public bool PathAllowed { get; init; }
    public IReadOnlyList<RobotsRule> Rules { get; init; } = Array.Empty<RobotsRule>();
}
