using System.Net;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Json.Path;

namespace MagicBox.Core;

public sealed record ExtractionResult
{
    public IReadOnlyList<JsonObject> Records { get; init; } = Array.Empty<JsonObject>();
    public string? NextUri { get; init; }
    public string? Cursor { get; init; }
}

public sealed class DocumentExtractor
{
    public async Task<ExtractionResult> ExtractAsync(
        AdapterDefinition adapter,
        FetchResponse response,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? inputs = null)
    {
        if (adapter.Response.Type.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractJson(adapter, response, inputs);
        }

        return await ExtractHtmlAsync(adapter, response, cancellationToken, inputs).ConfigureAwait(false);
    }

    private static async Task<ExtractionResult> ExtractHtmlAsync(
        AdapterDefinition adapter,
        FetchResponse response,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? inputs)
    {
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(response.Body, cancellationToken).ConfigureAwait(false);
        var selector = adapter.Extract.Items.Css ?? throw new InvalidDataException("HTML adapter is missing an item CSS selector.");
        var elements = document.QuerySelectorAll(selector);
        var records = new List<JsonObject>();

        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = new JsonObject();

            foreach (var field in adapter.Extract.Fields)
            {
                var target = string.IsNullOrWhiteSpace(field.Value.Selector)
                    ? element
                    : element.QuerySelector(field.Value.Selector!);
                var raw = ExtractHtmlValue(target, field.Value, response.FinalUri, inputs);
                record[field.Key] = ApplyTransforms(raw is null ? null : JsonValue.Create(raw), field.Value.Transforms, response.FinalUri);
            }

            records.Add(record);
        }

        string? nextUri = null;
        if (adapter.Pagination.Strategy.Equals("nextLink", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(adapter.Pagination.Selector))
        {
            var next = document.QuerySelector(adapter.Pagination.Selector!);
            var href = next?.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(response.FinalUri, href, out var resolved))
            {
                nextUri = resolved.AbsoluteUri;
            }
        }

        return new ExtractionResult { Records = records, NextUri = nextUri };
    }

    private static ExtractionResult ExtractJson(AdapterDefinition adapter, FetchResponse response, IReadOnlyDictionary<string, string>? inputs)
    {
        var root = JsonNode.Parse(response.Body) ?? throw new InvalidDataException("JSON response was empty.");
        var itemPath = adapter.Extract.Items.JsonPath ?? throw new InvalidDataException("JSON adapter is missing an item JSONPath.");
        var items = Evaluate(root, itemPath);
        var records = new List<JsonObject>();

        foreach (var item in items)
        {
            var record = new JsonObject();
            foreach (var field in adapter.Extract.Fields)
            {
                JsonNode? valueNode;
                var source = field.Value.Source.ToLowerInvariant();
                if (source == "constant")
                {
                    valueNode = field.Value.Value is null ? null : JsonValue.Create(field.Value.Value);
                }
                else if (source == "input")
                {
                    valueNode = field.Value.Input is not null && inputs is not null && inputs.TryGetValue(field.Value.Input, out var input) ? JsonValue.Create(input) : null;
                }
                else if (source is "currenturl" or "sourceurl")
                {
                    valueNode = JsonValue.Create(response.FinalUri.AbsoluteUri);
                }
                else if (!string.IsNullOrWhiteSpace(field.Value.JsonPath))
                {
                    valueNode = Evaluate(item ?? new JsonObject(), field.Value.JsonPath!).FirstOrDefault();
                }
                else
                {
                    valueNode = item;
                }

                record[field.Key] = ApplyTransforms(valueNode?.DeepClone(), field.Value.Transforms, response.FinalUri);
            }

            records.Add(record);
        }

        string? cursor = null;
        if (adapter.Pagination.Strategy.Equals("jsonCursor", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(adapter.Pagination.CursorPath))
        {
            cursor = ToText(Evaluate(root, adapter.Pagination.CursorPath!).FirstOrDefault());
        }

        return new ExtractionResult { Records = records, Cursor = cursor };
    }

    private static string? ExtractHtmlValue(IElement? element, FieldDefinition definition, Uri sourceUri, IReadOnlyDictionary<string, string>? inputs)
    {
        var source = definition.Source.ToLowerInvariant();
        if (source == "constant")
        {
            return definition.Value;
        }

        if (source == "input")
        {
            return definition.Input is not null && inputs is not null && inputs.TryGetValue(definition.Input, out var input) ? input : null;
        }

        if (source is "currenturl" or "sourceurl")
        {
            return sourceUri.AbsoluteUri;
        }

        if (element is null)
        {
            return null;
        }

        return definition.Source.ToLowerInvariant() switch
        {
            "text" => element.TextContent,
            "attribute" when !string.IsNullOrWhiteSpace(definition.Attribute) => element.GetAttribute(definition.Attribute!),
            "innerhtml" => element.InnerHtml,
            "outerhtml" => element.OuterHtml,
            _ => element.TextContent
        };
    }

    private static IReadOnlyList<JsonNode?> Evaluate(JsonNode node, string path)
    {
        var compiled = JsonPath.Parse(path);
        return compiled.Evaluate(node).Matches.Select(match => match.Value).ToArray();
    }

    private static string? ToText(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        if (node is JsonValue scalar)
        {
            return scalar.ToJsonString().Trim('"');
        }

        return node.ToJsonString();
    }

    private static JsonNode? ApplyTransforms(JsonNode? current, IEnumerable<TransformDefinition> transforms, Uri sourceUri)
    {
        foreach (var transform in transforms)
        {
            var kind = transform.Kind.Trim().ToLowerInvariant();
            if (current is null && kind is not "default" and not "required" and not "nonempty")
            {
                continue;
            }

            var text = ToText(current);
            current = kind switch
            {
                "trim" => StringValue(text?.Trim()),
                "whitespacenormalize" or "normalizewhitespace" => StringValue(NormalizeWhitespace(text)),
                "htmldecode" => StringValue(text is null ? null : WebUtility.HtmlDecode(text)),
                "urldecode" => StringValue(text is null ? null : WebUtility.UrlDecode(text)),
                "urlencode" => StringValue(text is null ? null : Uri.EscapeDataString(text)),
                "urlresolve" => StringValue(ResolveUrl(text, sourceUri)),
                "queryparameter" => StringValue(ExtractQueryParameter(text, transform.Argument)),
                "yahooredirect" => StringValue(ExtractYahooRedirect(text)),
                "requireabsoluteuri" or "uri" => StringValue(RequireAbsoluteUri(text)),
                "regexcapture" => StringValue(RegexCapture(text, transform.Pattern)),
                "replace" => StringValue(text?.Replace(transform.Pattern ?? string.Empty, transform.Replacement ?? string.Empty, StringComparison.Ordinal)),
                "lower" => StringValue(text?.ToLowerInvariant()),
                "upper" => StringValue(text?.ToUpperInvariant()),
                "split" => SplitValue(text, transform.Argument),
                "join" => JoinValue(current, transform.Argument),
                "integer" or "int" => IntegerValue(text, transform.Kind),
                "decimal" => DecimalValue(text, transform.Kind),
                "boolean" or "bool" => BooleanValue(text, transform.Kind),
                "date" or "datetime" => DateValue(text, transform.Kind),
                "distinct" or "deduplicate" => DistinctValue(current),
                "required" or "nonempty" => RequiredValue(current, transform.Kind),
                "default" => string.IsNullOrWhiteSpace(text) ? StringValue(transform.Argument) : current,
                _ => throw new InvalidDataException($"Unsupported transform '{transform.Kind}'.")
            };
        }

        return current;
    }

    private static JsonNode? StringValue(string? value) => value is null ? null : JsonValue.Create(value);

    private static string? NormalizeWhitespace(string? value) => value is null ? null : Regex.Replace(value.Trim(), "\\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    private static JsonNode? SplitValue(string? value, string? separator)
    {
        if (value is null)
        {
            return null;
        }

        var delimiter = string.IsNullOrEmpty(separator) ? "," : separator;
        return new JsonArray(value.Split(delimiter, StringSplitOptions.None).Select(item => (JsonNode?)JsonValue.Create(item.Trim())).ToArray());
    }

    private static JsonNode? JoinValue(JsonNode? value, string? separator)
    {
        if (value is not JsonArray array)
        {
            return StringValue(ToText(value));
        }

        var delimiter = separator ?? ",";
        return StringValue(string.Join(delimiter, array.Select(ToText)));
    }

    private static JsonNode? IntegerValue(string? value, string kind)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Transform '{kind}' could not convert '{value}' to an integer.");
        }

        return JsonValue.Create(result);
    }

    private static JsonNode? DecimalValue(string? value, string kind)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException($"Transform '{kind}' could not convert '{value}' to a decimal.");
        }

        return JsonValue.Create(result);
    }

    private static JsonNode? BooleanValue(string? value, string kind)
    {
        if (bool.TryParse(value, out var result))
        {
            return JsonValue.Create(result);
        }

        if (value is "0" or "1")
        {
            return JsonValue.Create(value == "1");
        }

        throw new InvalidDataException($"Transform '{kind}' could not convert '{value}' to a Boolean.");
    }

    private static JsonNode? DateValue(string? value, string kind)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
        {
            throw new InvalidDataException($"Transform '{kind}' could not convert '{value}' to a date.");
        }

        return JsonValue.Create(result.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    private static JsonNode? DistinctValue(JsonNode? value)
    {
        if (value is not JsonArray array)
        {
            return value;
        }

        var unique = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array)
        {
            var key = item?.ToJsonString() ?? "null";
            if (seen.Add(key))
            {
                unique.Add(item?.DeepClone());
            }
        }

        return unique;
    }

    private static JsonNode? RequiredValue(JsonNode? value, string kind)
    {
        if (string.IsNullOrWhiteSpace(ToText(value)))
        {
            throw new InvalidDataException($"Transform '{kind}' rejected an empty value.");
        }

        return value;
    }

    private static string? ResolveUrl(string? value, Uri sourceUri) =>
        value is not null && Uri.TryCreate(sourceUri, value, out var resolved) ? resolved.AbsoluteUri : null;

    private static string? RequireAbsoluteUri(string? value) =>
        value is not null && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;

    private static string? ExtractQueryParameter(string? value, string? name)
    {
        if (value is null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var query = value;
        var questionMark = query.IndexOf('?');
        if (questionMark >= 0)
        {
            query = query[(questionMark + 1)..];
        }
        else if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            query = uri.Query;
        }

        foreach (var pair in query.TrimStart('?', '&').Replace("&amp;", "&", StringComparison.Ordinal).Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0 && pair[..separator].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.UrlDecode(pair[(separator + 1)..]);
            }
        }

        return null;
    }

    private static string? ExtractYahooRedirect(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var start = value.IndexOf("/RU=", StringComparison.OrdinalIgnoreCase);
        var end = value.IndexOf("/RK", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end <= start)
        {
            return value;
        }

        return WebUtility.UrlDecode(value.Substring(start + 4, end - start - 4));
    }

    private static string? RegexCapture(string? value, string? pattern)
    {
        if (value is null || string.IsNullOrWhiteSpace(pattern))
        {
            return value;
        }

        var match = Regex.Match(value, pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(250));
        return match.Success
            ? match.Groups.Count > 1 ? match.Groups[1].Value : match.Value
            : null;
    }
}
