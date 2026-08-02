using MagicBox.Core;

namespace MagicBox.Tests;

public sealed class BuiltInAdapterFixtureTests
{
    public static IEnumerable<object[]> Adapters()
    {
        foreach (var id in new[] { "google", "yahoo", "bing", "aol", "duckduckgo", "brave" })
        {
            yield return new object[] { id };
        }
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task MigratedSearchAdapterExtractsCanonicalResult(string adapterId)
    {
        var registry = AdapterRegistry.Load(new[] { TestPaths.Root("adapters", "builtin") }, TestPaths.Root("schemas", "adapter-v1.schema.json"));
        var adapter = registry.Get(adapterId);
        var response = new FetchResponse
        {
            Body = await File.ReadAllTextAsync(TestPaths.Root("tests", "fixtures", adapterId + ".html")),
            FinalUri = new Uri(adapter.Request.Url)
        };

        var extraction = await new DocumentExtractor().ExtractAsync(adapter, response, CancellationToken.None);

        var record = Assert.Single(extraction.Records);
        var expectedUrl = adapterId switch
        {
            "google" => "https://google.example/result",
            "yahoo" => "https://yahoo.example/result",
            "bing" => "https://bing.example/result",
            "aol" => "https://aol.example/result",
            "duckduckgo" => "https://duck.example/result",
            "brave" => "https://brave.example/result",
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(expectedUrl, record["url"]?.GetValue<string>() ?? record.ToJsonString());
        Assert.False(string.IsNullOrWhiteSpace(record["title"]?.GetValue<string>()));
    }
}
