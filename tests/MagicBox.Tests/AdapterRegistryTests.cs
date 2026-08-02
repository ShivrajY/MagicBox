using MagicBox.Core;

namespace MagicBox.Tests;

public sealed class AdapterRegistryTests
{
    [Fact]
    public void BuiltInAdaptersAreSchemaValidatedAndUnique()
    {
        var registry = AdapterRegistry.Load(new[] { TestPaths.Root("adapters", "builtin") }, TestPaths.Root("schemas", "adapter-v1.schema.json"));

        Assert.Equal(6, registry.All.Count);
        Assert.Equal(new[] { "aol", "bing", "brave", "duckduckgo", "google", "yahoo" }, registry.All.Select(adapter => adapter.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicateIdsAreRejected()
    {
        var directory = Directory.CreateTempSubdirectory("magicbox-adapter-");
        try
        {
            var first = Path.Combine(directory.FullName, "one.yaml");
            var second = Path.Combine(directory.FullName, "two.yaml");
            File.Copy(TestPaths.Root("adapters", "builtin", "bing.yaml"), first);
            File.Copy(TestPaths.Root("adapters", "builtin", "bing.yaml"), second);

            var error = Assert.Throws<InvalidDataException>(() => AdapterRegistry.Load(new[] { directory.FullName }, TestPaths.Root("schemas", "adapter-v1.schema.json")));
            Assert.Contains("Duplicate adapter id", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void UnknownYamlPropertiesAreRejectedBeforeExecution()
    {
        var directory = Directory.CreateTempSubdirectory("magicbox-adapter-");
        try
        {
            var source = File.ReadAllText(TestPaths.Root("adapters", "builtin", "bing.yaml"));
            File.WriteAllText(Path.Combine(directory.FullName, "invalid.yaml"), source.Replace("kind: search", "kind: search\nunknownProperty: true", StringComparison.Ordinal));

            var error = Assert.Throws<InvalidDataException>(() => AdapterRegistry.Load(new[] { directory.FullName }, TestPaths.Root("schemas", "adapter-v1.schema.json")));
            Assert.Contains("schema validation", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

internal static class TestPaths
{
    public static string Root(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MagicBox.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(new[] { directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.") }.Concat(parts).ToArray());
    }
}
