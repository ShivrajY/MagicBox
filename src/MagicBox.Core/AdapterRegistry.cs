using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MagicBox.Core;

public sealed class AdapterRegistry : IAdapterRegistry
{
    private readonly Dictionary<string, AdapterDefinition> adapters = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<AdapterDefinition> All => adapters.Values.ToArray();

    public AdapterDefinition Get(string id)
    {
        if (!adapters.TryGetValue(id, out var adapter))
        {
            throw new KeyNotFoundException($"Adapter '{id}' was not found.");
        }

        return adapter;
    }

    public static AdapterRegistry Load(IEnumerable<string> directories, string schemaPath)
    {
        var registry = new AdapterRegistry();
        var schema = JsonSchema.FromText(File.ReadAllText(schemaPath));

        foreach (var directory in directories.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal))
            {
                registry.AddFile(file, schema);
            }
        }

        return registry;
    }

    public void AddFile(string path, JsonSchema schema)
    {
        var yaml = File.ReadAllText(path);
        var yamlStream = new YamlStream();
        using (var reader = new StringReader(yaml))
        {
            yamlStream.Load(reader);
        }

        if (yamlStream.Documents.Count == 0)
        {
            throw new InvalidDataException($"Adapter '{path}' is empty.");
        }

        var jsonNode = ConvertYamlNode(yamlStream.Documents[0].RootNode) ?? throw new InvalidDataException($"Adapter '{path}' is empty.");
        var jsonText = jsonNode.ToJsonString();
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonNode.ToJsonString());
        var evaluation = schema.Evaluate(jsonElement);

        if (!evaluation.IsValid)
        {
            throw new InvalidDataException($"Adapter '{path}' failed schema validation.");
        }

        var adapter = JsonSerializer.Deserialize<AdapterDefinition>(jsonText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException($"Adapter '{path}' could not be decoded.");

        ValidateSemantics(adapter, path);
        if (!adapters.TryAdd(adapter.Id, adapter))
        {
            throw new InvalidDataException($"Duplicate adapter id '{adapter.Id}' in '{path}'.");
        }
    }

    private static JsonNode? ConvertYamlNode(YamlNode node)
    {
        return node switch
        {
            YamlMappingNode mapping => ConvertMapping(mapping),
            YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(child => ConvertYamlNode(child)).ToArray()),
            YamlScalarNode scalar => ConvertScalar(scalar),
            _ => throw new InvalidDataException($"Unsupported YAML node '{node.GetType().Name}'.")
        };
    }

    private static JsonObject ConvertMapping(YamlMappingNode mapping)
    {
        var result = new JsonObject();
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
            {
                throw new InvalidDataException("Adapter YAML mapping keys must be non-empty scalars.");
            }

            result[key.Value] = ConvertYamlNode(pair.Value);
        }

        return result;
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value);
        }

        if (value is "null" or "~")
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return JsonValue.Create(boolean);
        }

        if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var decimalValue))
        {
            return JsonValue.Create(decimalValue);
        }

        return JsonValue.Create(value);
    }

    private static void ValidateSemantics(AdapterDefinition adapter, string path)
    {
        if (adapter.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Adapter '{path}' has unsupported schema version {adapter.SchemaVersion}.");
        }

        if (!string.Equals(adapter.Kind, "search", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(adapter.Kind, "website", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Adapter '{path}' has unsupported kind '{adapter.Kind}'.");
        }

        if (adapter.Kind.Equals("search", StringComparison.OrdinalIgnoreCase)
            && (!adapter.Extract.Fields.ContainsKey("title") || !adapter.Extract.Fields.ContainsKey("url")))
        {
            throw new InvalidDataException($"Search adapter '{path}' must define title and url fields.");
        }

        if (!Uri.TryCreate(adapter.Request.Url, UriKind.Absolute, out var requestUri)
            || (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidDataException($"Adapter '{path}' must use an absolute HTTP or HTTPS request URL.");
        }

        if (adapter.Extract.Items.Css is null && adapter.Extract.Items.JsonPath is null)
        {
            throw new InvalidDataException($"Adapter '{path}' must define a CSS or JSONPath item selector.");
        }
    }
}
