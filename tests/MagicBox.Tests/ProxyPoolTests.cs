using MagicBox.Core;

namespace MagicBox.Tests;

public sealed class ProxyPoolTests
{
    [Fact]
    public void ProxyIsQuarantinedAfterThreeFailures()
    {
        var endpoint = new Uri("https://proxy.example.test:8443");
        var pool = new InMemoryProxyPool(new Dictionary<string, IReadOnlyList<Uri>>
        {
            ["test"] = new[] { endpoint }
        });

        Assert.Equal(endpoint, pool.Next("test"));
        pool.Report("test", endpoint, successful: false);
        pool.Report("test", endpoint, successful: false);
        pool.Report("test", endpoint, successful: false);

        Assert.Null(pool.Next("test"));
    }
}
