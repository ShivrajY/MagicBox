using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MagicBox.Core;

public enum ExecutionProfile
{
    Local,
    HighVolume
}

public enum ScrapeEventKind
{
    Record,
    Diagnostic,
    PageCompleted,
    RunCompleted
}

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public enum RenderMode
{
    Http,
    Browser,
    Auto
}

public sealed record ScrapeRequest
{
    public string AdapterId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Inputs { get; init; } = new Dictionary<string, string>();
    public ExecutionProfile Profile { get; init; } = ExecutionProfile.Local;
    public int MaxPages { get; init; } = 1;
    public int MaxRecords { get; init; } = 1000;
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public string? ProxyPoolName { get; init; }
    public bool AllowPrivateNetwork { get; init; }
}

public sealed record SearchRequest
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<string> EngineIds { get; init; } = Array.Empty<string>();
    public ExecutionProfile Profile { get; init; } = ExecutionProfile.Local;
    public int MaxPages { get; init; } = 1;
    public int MaxResults { get; init; } = 100;
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>();
    public string? Locale { get; init; }
    public string? Language { get; init; }
    public bool? SafeSearch { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public TimeSpan OverallTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public string? ProxyPoolName { get; init; }
    public bool AllowPrivateNetwork { get; init; }
}

public sealed record ScrapedRecord
{
    public string AdapterId { get; init; } = string.Empty;
    public Uri SourceUri { get; init; } = new("https://invalid.local/");
    public long Sequence { get; init; }
    public int Page { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
    public JsonObject Fields { get; init; } = new();
}

public sealed record SearchResult
{
    public string EngineId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public Uri Url { get; init; } = new("https://invalid.local/");
    public string? Snippet { get; init; }
    public int Rank { get; init; }
    public int Page { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
    public JsonObject Metadata { get; init; } = new();
}

public sealed record ScrapeDiagnostic
{
    public DiagnosticSeverity Severity { get; init; }
    public string Code { get; init; } = string.Empty;
    public string AdapterId { get; init; } = string.Empty;
    public Uri? SourceUri { get; init; }
    public int Attempt { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record ScrapeSummary
{
    public int PagesAttempted { get; init; }
    public int PagesCompleted { get; init; }
    public int RecordsEmitted { get; init; }
    public int RecordsRejected { get; init; }
    public int Retries { get; init; }
    public int HttpRequests { get; init; }
    public int BrowserRequests { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string Status { get; init; } = "completed";
}

public sealed record ScrapeEvent
{
    public ScrapeEventKind Kind { get; init; }
    public ScrapedRecord? Record { get; init; }
    public ScrapeDiagnostic? Diagnostic { get; init; }
    public ScrapeSummary? Summary { get; init; }
    public int Page { get; init; }

    public static ScrapeEvent ForRecord(ScrapedRecord record) => new()
    {
        Kind = ScrapeEventKind.Record,
        Record = record,
        Page = record.Page
    };

    public static ScrapeEvent ForDiagnostic(ScrapeDiagnostic diagnostic) => new()
    {
        Kind = ScrapeEventKind.Diagnostic,
        Diagnostic = diagnostic
    };

    public static ScrapeEvent ForPage(int page) => new()
    {
        Kind = ScrapeEventKind.PageCompleted,
        Page = page
    };

    public static ScrapeEvent ForSummary(ScrapeSummary summary) => new()
    {
        Kind = ScrapeEventKind.RunCompleted,
        Summary = summary
    };
}

public sealed record FetchRequest
{
    public Uri Uri { get; init; } = new("https://invalid.local/");
    public HttpMethod Method { get; init; } = HttpMethod.Get;
    public RenderMode RenderMode { get; init; } = RenderMode.Http;
    public IReadOnlyList<string> FallbackOn { get; init; } = Array.Empty<string>();
    public AuthDefinition? Auth { get; init; }
    public ISecretResolver? SecretResolver { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxBodyBytes { get; init; } = 10 * 1024 * 1024;
    public Uri? ProxyUri { get; init; }
    public bool AllowPrivateNetwork { get; init; }
    public bool Headed { get; init; }
    public ISessionStore? SessionStore { get; init; }
    public string? SessionPassphrase { get; init; }
    public PaginationDefinition? Pagination { get; init; }
    public string? RequiredSelector { get; init; }
}

public sealed record FetchResponse
{
    public int StatusCode { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    public string Body { get; init; } = string.Empty;
    public Uri FinalUri { get; init; } = new("https://invalid.local/");
    public string ContentType { get; init; } = string.Empty;
    public bool WasBrowserRendered { get; init; }
    public int RetryCount { get; init; }
}

public interface IFetcher
{
    Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken);
}

public interface IScrapeScheduler
{
    ValueTask<IAsyncDisposable> AcquireAsync(string host, ExecutionProfile profile, CancellationToken cancellationToken);
}

public interface IProxyPool
{
    Uri? Next(string poolName);
    void Report(string poolName, Uri proxyUri, bool successful);
}

public interface IAdapterRegistry
{
    IReadOnlyCollection<AdapterDefinition> All { get; }
    AdapterDefinition Get(string id);
}

public interface IChallengeHandler
{
    Task<bool> HandleAsync(string adapterId, Uri uri, CancellationToken cancellationToken);
}

public sealed class ChallengeRequiredException : InvalidOperationException
{
    public ChallengeRequiredException(string message) : base(message)
    {
    }
}

public interface ISecretResolver
{
    string? Resolve(string name);
}

public interface ISecretStore
{
    Task<Dictionary<string, string>> ReadAsync(string path, string passphrase, CancellationToken cancellationToken = default);
    Task WriteAsync(string path, IReadOnlyDictionary<string, string> secrets, string passphrase, CancellationToken cancellationToken = default);
}

public interface ISessionStore
{
    Task<string?> LoadAsync(string name, string passphrase, CancellationToken cancellationToken = default);
    Task SaveAsync(string name, string state, string passphrase, CancellationToken cancellationToken = default);
}

public interface IResultSink
{
    Task WriteAsync(ScrapeEvent scrapeEvent, CancellationToken cancellationToken);
}

public interface ISearchService
{
    IAsyncEnumerable<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);
}

public interface IRobotsReporter
{
    Task<RobotsResult> ReportAsync(Uri target, string userAgent, CancellationToken cancellationToken);
}
