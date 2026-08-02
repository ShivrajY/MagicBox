using MagicBox.Core;
using AngleSharp.Html.Parser;
using Microsoft.Playwright;

namespace MagicBox.Browser;

public sealed class BrowserFetcher : IFetcher, IAsyncDisposable
{
    private readonly HttpFetcher httpFetcher;
    private readonly IChallengeHandler challengeHandler;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private bool browserHeaded;

    public BrowserFetcher(IChallengeHandler? challengeHandler = null)
    {
        httpFetcher = new HttpFetcher();
        this.challengeHandler = challengeHandler ?? new ManualChallengeHandler();
    }

    public async Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
    {
        if (request.RenderMode == RenderMode.Http)
        {
            return await httpFetcher.FetchAsync(request, cancellationToken).ConfigureAwait(false);
        }

        if (request.RenderMode == RenderMode.Auto)
        {
            var httpResponse = await httpFetcher.FetchAsync(request with { RenderMode = RenderMode.Http }, cancellationToken).ConfigureAwait(false);
            if (!await ShouldFallbackAsync(request, httpResponse).ConfigureAwait(false))
            {
                return httpResponse;
            }
        }

        var needsHeaded = request.Headed || request.Auth?.Steps.Any(step => step.Kind.Equals("manualChallenge", StringComparison.OrdinalIgnoreCase)) == true;
        await EnsureBrowserAsync(needsHeaded, cancellationToken).ConfigureAwait(false);
        var contextOptions = new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = false,
            ExtraHTTPHeaders = request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
        if (request.Auth?.SessionName is { Length: > 0 } sessionName
            && request.SessionStore is not null
            && !string.IsNullOrEmpty(request.SessionPassphrase))
        {
            contextOptions.StorageState = await request.SessionStore.LoadAsync(sessionName, request.SessionPassphrase, cancellationToken).ConfigureAwait(false);
        }

        var context = await browser!.NewContextAsync(contextOptions).ConfigureAwait(false);

        try
        {
            var page = await context.NewPageAsync().ConfigureAwait(false);
            if (request.Auth is not null)
            {
                await new ConfiguredLoginRunner(challengeHandler).RunAsync(page, request.Auth, request.SecretResolver, request.Uri, cancellationToken).ConfigureAwait(false);
            }

            if (request.Auth?.SessionName is { Length: > 0 } savedSession
                && request.SessionStore is not null
                && !string.IsNullOrEmpty(request.SessionPassphrase))
            {
                var state = await context.StorageStateAsync(new BrowserContextStorageStateOptions()).ConfigureAwait(false);
                await request.SessionStore.SaveAsync(savedSession, state, request.SessionPassphrase, cancellationToken).ConfigureAwait(false);
            }

            var response = await page.GotoAsync(request.Uri.AbsoluteUri, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = (float)request.Timeout.TotalMilliseconds
            }).ConfigureAwait(false);

            if (request.Pagination?.Strategy.Equals("clickMore", StringComparison.OrdinalIgnoreCase) == true
                && !string.IsNullOrWhiteSpace(request.Pagination.Selector))
            {
                var maxIterations = Math.Clamp(request.Pagination.MaxPages, 1, 10_000);
                for (var iteration = 0; iteration < maxIterations; iteration++)
                {
                    var more = page.Locator(request.Pagination.Selector);
                    if (await more.CountAsync().ConfigureAwait(false) == 0)
                    {
                        break;
                    }

                    await more.Last.ClickAsync(new LocatorClickOptions { Timeout = (float)request.Timeout.TotalMilliseconds }).ConfigureAwait(false);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = (float)request.Timeout.TotalMilliseconds }).ConfigureAwait(false);
                }
            }

            var body = await page.ContentAsync().ConfigureAwait(false);
            return new FetchResponse
            {
                StatusCode = response?.Status ?? 200,
                Headers = response is null ? new Dictionary<string, string>() : await response.AllHeadersAsync().ConfigureAwait(false),
                Body = body,
                FinalUri = new Uri(page.Url),
                ContentType = "text/html",
                WasBrowserRendered = true
            };
        }
        finally
        {
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.CloseAsync().ConfigureAwait(false);
            browser = null;
        }

        playwright?.Dispose();
        playwright = null;
        httpFetcher.Dispose();
    }

    private async Task EnsureBrowserAsync(bool headed, CancellationToken cancellationToken)
    {
        if (browser is not null && browserHeaded == headed)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (browser is not null)
        {
            await browser.CloseAsync().ConfigureAwait(false);
            browser = null;
        }

        playwright?.Dispose();
        playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !headed
        }).ConfigureAwait(false);
        browserHeaded = headed;
    }

    private static Task<bool> ShouldFallbackAsync(FetchRequest request, FetchResponse response)
    {
        if (request.FallbackOn.Count == 0)
        {
            return Task.FromResult(false);
        }

        foreach (var condition in request.FallbackOn)
        {
            var normalized = condition.Trim().ToLowerInvariant();
            if (normalized is "javascript" or "js-shell" or "empty" && string.IsNullOrWhiteSpace(response.Body))
            {
                return Task.FromResult(true);
            }

            if (normalized.StartsWith("status:", StringComparison.Ordinal)
                && int.TryParse(normalized[7..], out var status)
                && status == response.StatusCode)
            {
                return Task.FromResult(true);
            }

            if (normalized == "5xx" && response.StatusCode is >= 500 and <= 599)
            {
                return Task.FromResult(true);
            }

            if (normalized is "javascript" or "js-shell"
                && response.Body.Contains("<script", StringComparison.OrdinalIgnoreCase)
                && !response.Body.Contains("<main", StringComparison.OrdinalIgnoreCase)
                && !response.Body.Contains("<article", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }

            if (normalized is "missingselector" or "missing-selector"
                && !string.IsNullOrWhiteSpace(request.RequiredSelector))
            {
                var document = new HtmlParser().ParseDocument(response.Body);
                if (document.QuerySelector(request.RequiredSelector) is null)
                {
                    return Task.FromResult(true);
                }
            }
        }

        return Task.FromResult(false);
    }
}
