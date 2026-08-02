namespace MagicBox.Core;

public sealed class SearchService : ISearchService
{
    private readonly ScrapeOrchestrator orchestrator;

    public SearchService(ScrapeOrchestrator orchestrator)
    {
        this.orchestrator = orchestrator;
    }

    public async IAsyncEnumerable<SearchResult> SearchAsync(SearchRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var engines = request.EngineIds.Count == 0 ? Array.Empty<string>() : request.EngineIds;
        var rank = 0;
        foreach (var engine in engines)
        {
            var options = new Dictionary<string, string>(request.Options, StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = request.Query
            };
            AddOption(options, "locale", request.Locale);
            AddOption(options, "language", request.Language);
            AddOption(options, "safeSearch", request.SafeSearch?.ToString());
            AddOption(options, "dateFrom", request.DateFrom?.ToString("O"));
            AddOption(options, "dateTo", request.DateTo?.ToString("O"));

            var scrapeRequest = new ScrapeRequest
            {
                AdapterId = engine,
                Inputs = options,
                Profile = request.Profile,
                MaxPages = request.MaxPages,
                MaxRecords = request.MaxResults,
                OverallTimeout = request.OverallTimeout,
                ProxyPoolName = request.ProxyPoolName,
                AllowPrivateNetwork = request.AllowPrivateNetwork
            };

            await foreach (var scrapeEvent in orchestrator.RunAsync(scrapeRequest, cancellationToken).ConfigureAwait(false))
            {
                if (scrapeEvent.Kind != ScrapeEventKind.Record || scrapeEvent.Record is null)
                {
                    continue;
                }

                var fields = scrapeEvent.Record.Fields;
                if (!Uri.TryCreate(fields["url"]?.ToString(), UriKind.Absolute, out var url))
                {
                    continue;
                }

                rank++;
                yield return new SearchResult
                {
                    EngineId = engine,
                    Title = fields["title"]?.ToString() ?? string.Empty,
                    Url = url,
                    Snippet = fields["snippet"]?.ToString(),
                    Rank = rank,
                    Page = scrapeEvent.Record.Page,
                    RetrievedAt = scrapeEvent.Record.RetrievedAt,
                    Metadata = fields
                };
            }
        }
    }

    private static void AddOption(IDictionary<string, string> options, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            options[name] = value;
        }
    }
}
