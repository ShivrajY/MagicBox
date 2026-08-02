using System.Net;

namespace MagicBox.Core;

internal static class TemplateExpander
{
    public static Uri BuildUri(AdapterDefinition adapter, IReadOnlyDictionary<string, string> inputs, int page, string? cursor = null)
    {
        var url = Expand(adapter.Request.Url, inputs, escape: true);
        var builder = new UriBuilder(url);
        var query = ParseQuery(builder.Query);

        foreach (var pair in adapter.Request.Query)
        {
            query[pair.Key] = Expand(pair.Value, inputs, escape: false);
        }

        var pagination = adapter.Pagination;
        if (pagination.Strategy.Equals("pageParameter", StringComparison.OrdinalIgnoreCase)
            || pagination.Strategy.Equals("offsetParameter", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(pagination.Parameter))
            {
                var number = pagination.Start + ((page - 1) * Math.Max(1, pagination.Step));
                query[pagination.Parameter] = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        if (pagination.Strategy.Equals("jsonCursor", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(pagination.Parameter)
            && cursor is not null)
        {
            query[pagination.Parameter] = cursor;
        }

        builder.Query = string.Join("&", query.Select(pair => $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));
        return builder.Uri;
    }

    public static string Expand(string template, IReadOnlyDictionary<string, string> inputs, bool escape = false)
    {
        var result = template;
        foreach (var pair in inputs)
        {
            var replacement = escape ? Uri.EscapeDataString(pair.Value) : pair.Value;
            result = result.Replace("${input." + pair.Key + "}", replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            values[WebUtility.UrlDecode(part[..separator])] = WebUtility.UrlDecode(part[(separator + 1)..]);
        }

        return values;
    }
}
