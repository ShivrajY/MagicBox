using System.Text.Json;

namespace MagicBox.Core;

public sealed class JsonLinesSink : IResultSink, IAsyncDisposable
{
    private readonly TextWriter writer;
    private readonly bool ownsWriter;

    public JsonLinesSink(TextWriter writer, bool ownsWriter = false)
    {
        this.writer = writer;
        this.ownsWriter = ownsWriter;
    }

    public async Task WriteAsync(ScrapeEvent scrapeEvent, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(scrapeEvent, JsonDefaults.Options);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await writer.FlushAsync().ConfigureAwait(false);
        if (ownsWriter)
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class CsvSink : IResultSink, IAsyncDisposable
{
    private readonly TextWriter writer;
    private readonly bool ownsWriter;
    private bool headerWritten;
    private IReadOnlyList<string> columns = Array.Empty<string>();

    public CsvSink(TextWriter writer, bool ownsWriter = false)
    {
        this.writer = writer;
        this.ownsWriter = ownsWriter;
    }

    public async Task WriteAsync(ScrapeEvent scrapeEvent, CancellationToken cancellationToken)
    {
        if (scrapeEvent.Kind != ScrapeEventKind.Record || scrapeEvent.Record is null)
        {
            return;
        }

        if (!headerWritten)
        {
            columns = scrapeEvent.Record.Fields.Select(pair => pair.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            await writer.WriteLineAsync(string.Join(',', columns.Select(Escape))).ConfigureAwait(false);
            headerWritten = true;
        }

        var values = columns.Select(column => ScalarToText(scrapeEvent.Record.Fields[column]));
        await writer.WriteLineAsync(string.Join(',', values.Select(Escape))).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await writer.FlushAsync().ConfigureAwait(false);
        if (ownsWriter)
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string ScalarToText(System.Text.Json.Nodes.JsonNode? value)
    {
        if (value is System.Text.Json.Nodes.JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            return text;
        }

        return value?.ToJsonString() ?? string.Empty;
    }
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
