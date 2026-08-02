using System.Text.Json.Nodes;
using MagicBox.Core;

namespace MagicBox.Tests;

public sealed class DocumentExtractorTests
{
    [Fact]
    public async Task HtmlCssExtractionAppliesTransforms()
    {
        var adapter = new AdapterDefinition
        {
            Id = "fixture-html",
            Kind = "website",
            Response = new ResponseDefinition { Type = "html" },
            Extract = new ExtractDefinition
            {
                Items = new SelectorDefinition { Css = "a.result" },
                Fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = new() { Source = "text", Transforms = new() { new TransformDefinition { Kind = "trim" }, new TransformDefinition { Kind = "htmldecode" } } },
                    ["url"] = new() { Source = "attribute", Attribute = "href", Transforms = new() { new TransformDefinition { Kind = "urlresolve" }, new TransformDefinition { Kind = "requireAbsoluteUri" } } }
                }
            }
        };
        var response = new FetchResponse
        {
            Body = await File.ReadAllTextAsync(TestPaths.Root("tests", "fixtures", "search.html")),
            FinalUri = new Uri("https://source.test/results")
        };

        var result = await new DocumentExtractor().ExtractAsync(adapter, response, CancellationToken.None);

        Assert.Equal(3, result.Records.Count);
        Assert.Equal("First result", result.Records[0]["title"]?.GetValue<string>());
        Assert.Equal("https://source.test/relative", result.Records[1]["url"]?.GetValue<string>());
        Assert.Equal("Third & result", result.Records[2]["title"]?.GetValue<string>());
    }

    [Fact]
    public async Task JsonPathExtractionReturnsTypedRecordFieldsAsStringsAtTheBoundary()
    {
        var adapter = new AdapterDefinition
        {
            Id = "fixture-json",
            Kind = "website",
            Response = new ResponseDefinition { Type = "json" },
            Extract = new ExtractDefinition
            {
                Items = new SelectorDefinition { JsonPath = "$.items[*]" },
                Fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = new() { Source = "json", JsonPath = "$.title", Transforms = new() { new TransformDefinition { Kind = "trim" } } },
                    ["url"] = new() { Source = "json", JsonPath = "$.url", Transforms = new() { new TransformDefinition { Kind = "requireAbsoluteUri" } } },
                    ["rank"] = new() { Source = "json", JsonPath = "$.rank" }
                }
            }
        };
        var response = new FetchResponse
        {
            Body = await File.ReadAllTextAsync(TestPaths.Root("tests", "fixtures", "records.json")),
            FinalUri = new Uri("https://source.test/results")
        };

        var result = await new DocumentExtractor().ExtractAsync(adapter, response, CancellationToken.None);

        Assert.Equal(2, result.Records.Count);
        Assert.Equal("First JSON result", result.Records[0]["title"]?.GetValue<string>());
        Assert.Equal(1, result.Records[0]["rank"]?.GetValue<int>());
    }
}
