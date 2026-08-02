namespace MagicBox.Core;

public sealed class DictionarySecretResolver : ISecretResolver
{
    private readonly IReadOnlyDictionary<string, string> values;

    public DictionarySecretResolver(IReadOnlyDictionary<string, string> values)
    {
        this.values = values;
    }

    public string? Resolve(string name) => values.TryGetValue(name, out var value) ? value : null;
}
