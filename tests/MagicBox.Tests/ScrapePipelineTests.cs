using System.Net;
using System.Net.Http.Headers;
using MagicBox.Core;

namespace MagicBox.Tests;

public sealed class ScrapePipelineTests
{
    [Fact]
    public async Task OrchestratorStreamsRecordsAndSummaryWithoutLiveNetwork()
    {
        var registry = AdapterRegistry.Load(new[] { TestPaths.Root("adapters", "builtin") }, TestPaths.Root("schemas", "adapter-v1.schema.json"));
        var fetcher = new FixtureFetcher(await File.ReadAllTextAsync(TestPaths.Root("tests", "fixtures", "search.html")));
        var orchestrator = new ScrapeOrchestrator(registry, fetcher, robotsReporter: new FixtureRobotsReporter());
        var events = new List<ScrapeEvent>();

        await foreach (var item in orchestrator.RunAsync(new ScrapeRequest
        {
            AdapterId = "bing",
            Inputs = new Dictionary<string, string> { ["query"] = "magicbox" },
            MaxPages = 1,
            MaxRecords = 2
        }))
        {
            events.Add(item);
        }

        Assert.Equal(2, events.Count(item => item.Kind == ScrapeEventKind.Record));
        Assert.Single(events, item => item.Kind == ScrapeEventKind.RunCompleted);
        Assert.Equal(2, events.Single(item => item.Kind == ScrapeEventKind.RunCompleted).Summary!.RecordsEmitted);
        Assert.Equal(1, fetcher.Requests);
    }

    private sealed class FixtureFetcher : IFetcher
    {
        private readonly string body;
        public int Requests { get; private set; }

        public FixtureFetcher(string body) => this.body = body;

        public Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new FetchResponse
            {
                StatusCode = 200,
                Body = body,
                FinalUri = request.Uri,
                ContentType = "text/html"
            });
        }
    }

    private sealed class FixtureRobotsReporter : IRobotsReporter
    {
        public Task<RobotsResult> ReportAsync(Uri target, string userAgent, CancellationToken cancellationToken) =>
            Task.FromResult(new RobotsResult { RobotsUri = new Uri(target, "/robots.txt"), Retrieved = false, PathAllowed = true });
    }
}

public sealed class HttpFetcherTests
{
    [Fact]
    public async Task RetryableStatusIsRetriedAndBodyIsReturned()
    {
        var handler = new SequenceHandler();
        using var client = new HttpClient(handler);
        using var fetcher = new HttpFetcher(client);

        var response = await fetcher.FetchAsync(new FetchRequest
        {
            Uri = new Uri("https://example.test/"),
            Timeout = TimeSpan.FromSeconds(2)
        }, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("ok", response.Body);
        Assert.Equal(2, handler.Requests);
        Assert.Equal(1, response.RetryCount);
    }

    [Fact]
    public async Task PrivateNetworkTargetsRequireExplicitOptIn()
    {
        using var fetcher = new HttpFetcher(new HttpClient(new SequenceHandler()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fetcher.FetchAsync(new FetchRequest { Uri = new Uri("http://127.0.0.1/") }, CancellationToken.None));
    }

    [Fact]
    public async Task RedirectToPrivateNetworkIsRejected()
    {
        using var fetcher = new HttpFetcher(new HttpClient(new RedirectHandler()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fetcher.FetchAsync(new FetchRequest { Uri = new Uri("https://example.test/") }, CancellationToken.None));
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Requests == 1)
            {
                var retry = new HttpResponseMessage((HttpStatusCode)429);
                retry.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(retry);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
        }
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("http://127.0.0.1/private") }
            });
    }
}
