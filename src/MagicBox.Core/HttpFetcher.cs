using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace MagicBox.Core;

public sealed class HttpFetcher : IFetcher, IDisposable
{
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly ConcurrentDictionary<string, HttpClient> proxyClients = new(StringComparer.OrdinalIgnoreCase);

    public HttpFetcher(HttpClient? client = null)
    {
        if (client is not null)
        {
            this.client = client;
            ownsClient = false;
            return;
        }

        var handler = CreateHandler(null);
        this.client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        this.client.DefaultRequestHeaders.UserAgent.ParseAdd("MagicBox/1.0 (+https://github.com/ShivrajY/MagicBox)");
        ownsClient = true;
    }

    public async Task<FetchResponse> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
    {
        await ValidateUriAsync(request.Uri, request.AllowPrivateNetwork, cancellationToken).ConfigureAwait(false);
        var attempts = 0;
        Exception? lastException = null;

        while (attempts < 4)
        {
            attempts++;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.Timeout);

            try
            {
                var activeClient = GetClient(request);
                var responseAndUri = await SendFollowingRedirectsAsync(activeClient, request, timeout.Token).ConfigureAwait(false);
                using var response = responseAndUri.Response;
                var body = await ReadBodyAsync(response.Content, request.MaxBodyBytes, timeout.Token).ConfigureAwait(false);
                var headers = response.Headers.Concat(response.Content.Headers)
                    .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => string.Join(", ", pair.SelectMany(entry => entry.Value)), StringComparer.OrdinalIgnoreCase);

                if (ShouldRetry(response.StatusCode) && attempts < 4)
                {
                    await DelayBeforeRetryAsync(response.Headers.RetryAfter, attempts, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return new FetchResponse
                {
                    StatusCode = (int)response.StatusCode,
                    Headers = headers,
                    Body = body,
                    FinalUri = responseAndUri.FinalUri,
                    ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                    WasBrowserRendered = false,
                    RetryCount = attempts - 1
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempts < 4)
            {
                lastException = new TimeoutException($"Request timed out after {request.Timeout}.");
                await DelayBeforeRetryAsync(null, attempts, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempts < 4)
            {
                lastException = ex;
                await DelayBeforeRetryAsync(null, attempts, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new HttpRequestException("HTTP request failed after retries.", lastException);
    }

    public void Dispose()
    {
        if (ownsClient)
        {
            client.Dispose();
        }

        foreach (var proxyClient in proxyClients.Values)
        {
            proxyClient.Dispose();
        }
    }

    private static async Task<string> ReadBodyAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidDataException($"Response exceeded the {maxBytes} byte limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task<(HttpResponseMessage Response, Uri FinalUri)> SendFollowingRedirectsAsync(HttpClient activeClient, FetchRequest request, CancellationToken cancellationToken)
    {
        var currentUri = request.Uri;
        for (var redirect = 0; redirect <= 10; redirect++)
        {
            await ValidateUriAsync(currentUri, request.AllowPrivateNetwork, cancellationToken).ConfigureAwait(false);
            using var message = new HttpRequestMessage(request.Method, currentUri);
            foreach (var header in request.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    message.Content ??= new StringContent(string.Empty);
                    message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            var response = await activeClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is not (HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
            {
                return (response, currentUri);
            }

            if (response.Headers.Location is not { } location)
            {
                return (response, currentUri);
            }

            if (redirect == 10)
            {
                response.Dispose();
                throw new HttpRequestException("HTTP redirect limit exceeded.");
            }

            var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
            await ValidateUriAsync(nextUri, request.AllowPrivateNetwork, cancellationToken).ConfigureAwait(false);
            response.Dispose();
            currentUri = nextUri;
        }

        throw new HttpRequestException("HTTP redirect processing failed.");
    }

    private HttpClient GetClient(FetchRequest request)
    {
        if (request.ProxyUri is null)
        {
            return client;
        }

        if (request.ProxyUri.UserInfo.Length > 0)
        {
            throw new InvalidOperationException("Inline proxy credentials are not permitted; use a logical secret reference.");
        }

        if (request.ProxyUri.Scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("SOCKS5 proxy transport requires an application-supplied proxy handler in v1.");
        }

        return proxyClients.GetOrAdd(request.ProxyUri.AbsoluteUri, _ =>
        {
            var proxyClient = new HttpClient(CreateHandler(request.ProxyUri)) { Timeout = Timeout.InfiniteTimeSpan };
            proxyClient.DefaultRequestHeaders.UserAgent.ParseAdd("MagicBox/1.0 (+https://github.com/ShivrajY/MagicBox)");
            return proxyClient;
        });
    }

    private static SocketsHttpHandler CreateHandler(Uri? proxyUri)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        if (proxyUri is not null)
        {
            handler.Proxy = new WebProxy(proxyUri);
            handler.UseProxy = true;
        }

        return handler;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout
        || statusCode == (HttpStatusCode)429
        || (int)statusCode >= 500;

    private static async Task DelayBeforeRetryAsync(RetryConditionHeaderValue? retryAfter, int attempt, CancellationToken cancellationToken)
    {
        var retryDelay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? (TimeSpan?)(date - DateTimeOffset.UtcNow) : null)
            ?? TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
        retryDelay = retryDelay < TimeSpan.Zero ? TimeSpan.Zero : retryDelay;
        var jitter = Random.Shared.Next(0, 250);
        await Task.Delay(retryDelay + TimeSpan.FromMilliseconds(jitter), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ValidateUriAsync(Uri uri, bool allowPrivateNetwork, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Unsupported URI scheme '{uri.Scheme}'.");
        }

        if (!allowPrivateNetwork && (NetworkPolicy.IsPrivateOrLocal(uri.Host) || await NetworkPolicy.ResolvesToPrivateAddressAsync(uri.Host, cancellationToken).ConfigureAwait(false)))
        {
            throw new InvalidOperationException($"Private or local network target '{uri.Host}' requires explicit authorization.");
        }
    }
}

internal static class NetworkPolicy
{
    public static bool IsPrivateOrLocal(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("localhost.localdomain", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        return (bytes[0] & 0xfe) == 0xfc || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80);
    }

    public static async Task<bool> ResolvesToPrivateAddressAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out _))
        {
            return false;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            return addresses.Any(address => IsPrivateOrLocal(address.ToString()));
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
