using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace MagicBox.Core;

public sealed class ScrapeOrchestrator
{
    private readonly IAdapterRegistry registry;
    private readonly IFetcher fetcher;
    private readonly DocumentExtractor extractor;
    private readonly IRobotsReporter robotsReporter;
    private readonly IScrapeScheduler limiter;
    private readonly ISecretResolver? secretResolver;
    private readonly IProxyPool? proxyPool;

    public ScrapeOrchestrator(
        IAdapterRegistry registry,
        IFetcher fetcher,
        DocumentExtractor? extractor = null,
        IRobotsReporter? robotsReporter = null,
        IScrapeScheduler? limiter = null,
        ISecretResolver? secretResolver = null,
        IProxyPool? proxyPool = null)
    {
        this.registry = registry;
        this.fetcher = fetcher;
        this.extractor = extractor ?? new DocumentExtractor();
        this.robotsReporter = robotsReporter ?? new RobotsReporter();
        this.limiter = limiter ?? new RequestLimiter();
        this.secretResolver = secretResolver;
        this.proxyPool = proxyPool;
    }

    public async IAsyncEnumerable<ScrapeEvent> RunAsync(
        ScrapeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var adapter = registry.Get(request.AdapterId);
        var inputs = ResolveInputs(adapter, request.Inputs);
        var maxPages = Math.Clamp(Math.Min(request.MaxPages, adapter.Pagination.MaxPages), 1, 10_000);
        var maxRecords = Math.Clamp(request.MaxRecords, 1, 1_000_000);
        using var overallTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallTimeout.CancelAfter(request.OverallTimeout);
        var token = overallTimeout.Token;
        var seenUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sequence = 0L;
        var emitted = 0;
        var rejected = 0;
        var pagesCompleted = 0;
        var retries = 0;
        var httpRequests = 0;
        var browserRequests = 0;
        var page = 1;
        var finalStatus = "completed";
        string? nextUri = null;
        string? cursor = null;

        while (page <= maxPages && emitted < maxRecords)
        {
            token.ThrowIfCancellationRequested();
            var pageUri = nextUri is not null
                ? new Uri(nextUri)
                : TemplateExpander.BuildUri(adapter, inputs, page, cursor);

            if (!seenUris.Add(pageUri.AbsoluteUri))
            {
                finalStatus = "partial";
                yield return ScrapeEvent.ForDiagnostic(Diagnostic(adapter.Id, "duplicate-page", DiagnosticSeverity.Warning, pageUri, "Pagination produced a duplicate page URI."));
                break;
            }

            var proxyUri = request.ProxyPoolName is null ? null : proxyPool?.Next(request.ProxyPoolName);
            await using var lease = await limiter.AcquireAsync($"{pageUri.Host}|{proxyUri}", request.Profile, token).ConfigureAwait(false);
            var robots = await robotsReporter.ReportAsync(pageUri, "MagicBox", token).ConfigureAwait(false);
            yield return ScrapeEvent.ForDiagnostic(Diagnostic(adapter.Id, robots.PathAllowed ? "robots-allowed" : "robots-disallowed", DiagnosticSeverity.Info, pageUri, robots.PathAllowed ? "robots.txt reports this path as allowed." : "robots.txt reports this path as disallowed; report-only policy continues."));

            var fetchRequest = new FetchRequest
            {
                Uri = pageUri,
                Method = new HttpMethod(adapter.Request.Method),
                RenderMode = ParseRenderMode(adapter.Render.Mode),
                FallbackOn = adapter.Render.FallbackOn,
                Auth = adapter.Auth,
                SecretResolver = secretResolver,
                Headers = adapter.Request.Headers,
                Timeout = TimeSpan.FromMilliseconds(adapter.Request.TimeoutMs),
                MaxBodyBytes = adapter.Request.MaxBodyBytes,
                AllowPrivateNetwork = request.AllowPrivateNetwork,
                Pagination = adapter.Pagination,
                RequiredSelector = adapter.Extract.Items.Css,
                ProxyUri = proxyUri
            };

            FetchResponse? response = null;
            Exception? fetchError = null;
            try
            {
                response = await fetcher.FetchAsync(fetchRequest, token).ConfigureAwait(false);
                if (proxyUri is not null && request.ProxyPoolName is not null)
                {
                    proxyPool?.Report(request.ProxyPoolName, proxyUri, successful: true);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (proxyUri is not null && request.ProxyPoolName is not null)
                {
                    proxyPool?.Report(request.ProxyPoolName, proxyUri, successful: false);
                }
                fetchError = ex;
            }

            if (fetchError is not null)
            {
                finalStatus = fetchError is ChallengeRequiredException ? "challenge-required" : "failed";
                yield return ScrapeEvent.ForDiagnostic(Diagnostic(adapter.Id, fetchError is ChallengeRequiredException ? "ChallengeRequired" : "fetch-failed", DiagnosticSeverity.Error, pageUri, SanitizeError(fetchError.Message)));
                break;
            }

            if (response is null)
            {
                finalStatus = "failed";
                yield return ScrapeEvent.ForDiagnostic(Diagnostic(adapter.Id, "fetch-failed", DiagnosticSeverity.Error, pageUri, "Fetcher returned no response."));
                break;
            }

            if (response.WasBrowserRendered)
            {
                browserRequests++;
            }
            else
            {
                httpRequests += 1 + response.RetryCount;
            }

            retries += response.RetryCount;

            if (response.StatusCode >= 400)
            {
                finalStatus = "partial";
                yield return ScrapeEvent.ForDiagnostic(Diagnostic(adapter.Id, "http-error", DiagnosticSeverity.Error, pageUri, $"Request returned HTTP {response.StatusCode}."));
                break;
            }

            var extracted = await extractor.ExtractAsync(adapter, response, token, inputs).ConfigureAwait(false);
            foreach (var fields in extracted.Records)
            {
                if (emitted >= maxRecords)
                {
                    break;
                }

                if (!HasRequiredFields(fields, adapter.Policy.RequiredFields))
                {
                    rejected++;
                    yield return ScrapeEvent.ForDiagnostic(Diagnostic(adapter.Id, "required-field-missing", DiagnosticSeverity.Warning, pageUri, "Record was rejected because a required field was empty."));
                    continue;
                }

                var record = new ScrapedRecord
                {
                    AdapterId = adapter.Id,
                    SourceUri = response.FinalUri,
                    Sequence = ++sequence,
                    Page = page,
                    RetrievedAt = DateTimeOffset.UtcNow,
                    Fields = fields
                };
                emitted++;
                yield return ScrapeEvent.ForRecord(record);
            }

            pagesCompleted++;
            yield return ScrapeEvent.ForPage(page);
            nextUri = extracted.NextUri;
            cursor = extracted.Cursor;

            var strategy = adapter.Pagination.Strategy;
            if (strategy.Equals("none", StringComparison.OrdinalIgnoreCase)
                || strategy.Equals("clickMore", StringComparison.OrdinalIgnoreCase)
                || (strategy.Equals("nextLink", StringComparison.OrdinalIgnoreCase) && nextUri is null)
                || (strategy.Equals("jsonCursor", StringComparison.OrdinalIgnoreCase) && cursor is null))
            {
                break;
            }

            page++;
        }

        yield return ScrapeEvent.ForSummary(new ScrapeSummary
        {
            PagesAttempted = page,
            PagesCompleted = pagesCompleted,
            RecordsEmitted = emitted,
            RecordsRejected = rejected,
            Retries = retries,
            HttpRequests = httpRequests,
            BrowserRequests = browserRequests,
            Elapsed = DateTimeOffset.UtcNow - started,
            Status = cancellationToken.IsCancellationRequested ? "cancelled" : finalStatus
        });
    }

    private static IReadOnlyDictionary<string, string> ResolveInputs(AdapterDefinition adapter, IReadOnlyDictionary<string, string> inputs)
    {
        var resolved = new Dictionary<string, string>(inputs, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in adapter.Inputs)
        {
            if (!resolved.TryGetValue(pair.Key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                if (!string.IsNullOrWhiteSpace(pair.Value.Default))
                {
                    resolved[pair.Key] = pair.Value.Default;
                    continue;
                }

                if (pair.Value.Required)
                {
                    throw new ArgumentException($"Required input '{pair.Key}' was not supplied.");
                }
            }
        }

        return resolved;
    }

    private static bool HasRequiredFields(JsonObject fields, IEnumerable<string> required)
    {
        return required.All(field => fields.TryGetPropertyValue(field, out var value) && !string.IsNullOrWhiteSpace(value?.ToJsonString().Trim('"')));
    }

    private static RenderMode ParseRenderMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "browser" => RenderMode.Browser,
            "auto" => RenderMode.Auto,
            _ => RenderMode.Http
        };

    private static ScrapeDiagnostic Diagnostic(string adapterId, string code, DiagnosticSeverity severity, Uri? uri, string message) => new()
    {
        AdapterId = adapterId,
        Code = code,
        Severity = severity,
        SourceUri = uri,
        Message = message
    };

    private static string SanitizeError(string message)
    {
        var sanitized = System.Text.RegularExpressions.Regex.Replace(message, "(?i)(password|secret|token|authorization)\\s*[=:]\\s*[^\\s,;]+", "$1=[redacted]", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        return sanitized.Length <= 500 ? sanitized : sanitized[..500];
    }
}

public sealed class RequestLimiter : IScrapeScheduler
{
    private readonly SemaphoreSlim globalLocal = new(8, 8);
    private readonly SemaphoreSlim globalHigh = new(64, 64);
    private readonly ConcurrentDictionary<string, HostGate> hosts = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(string host, ExecutionProfile profile, CancellationToken cancellationToken)
    {
        var global = profile == ExecutionProfile.HighVolume ? globalHigh : globalLocal;
        var hostGate = hosts.GetOrAdd(host, _ => new HostGate(profile == ExecutionProfile.HighVolume ? 4 : 2, profile == ExecutionProfile.HighVolume ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromSeconds(1)));
        await global.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await hostGate.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            await hostGate.WaitForIntervalAsync(cancellationToken).ConfigureAwait(false);
            return new Lease(global, hostGate.Gate);
        }
        catch
        {
            global.Release();
            throw;
        }
    }

    private sealed class HostGate
    {
        public SemaphoreSlim Gate { get; }
        private readonly TimeSpan interval;
        private DateTimeOffset lastRequest = DateTimeOffset.MinValue;
        private readonly object sync = new();

        public HostGate(int concurrency, TimeSpan interval)
        {
            Gate = new SemaphoreSlim(concurrency, concurrency);
            this.interval = interval;
        }

        public async Task WaitForIntervalAsync(CancellationToken cancellationToken)
        {
            TimeSpan wait;
            lock (sync)
            {
                var next = lastRequest + interval;
                wait = next > DateTimeOffset.UtcNow ? next - DateTimeOffset.UtcNow : TimeSpan.Zero;
                lastRequest = DateTimeOffset.UtcNow + wait;
            }

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly SemaphoreSlim global;
        private readonly SemaphoreSlim host;
        private int released;

        public Lease(SemaphoreSlim global, SemaphoreSlim host)
        {
            this.global = global;
            this.host = host;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                host.Release();
                global.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
