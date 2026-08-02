using System.Collections.Generic;

namespace MagicBox.Core;

public sealed class AdapterDefinition
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "website";
    public Dictionary<string, InputDefinition> Inputs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public RequestDefinition Request { get; set; } = new();
    public RenderDefinition Render { get; set; } = new();
    public ResponseDefinition Response { get; set; } = new();
    public ExtractDefinition Extract { get; set; } = new();
    public PaginationDefinition Pagination { get; set; } = new();
    public PolicyDefinition Policy { get; set; } = new();
    public AuthDefinition? Auth { get; set; }
}

public sealed class InputDefinition
{
    public bool Required { get; set; }
    public string Type { get; set; } = "string";
    public string? Default { get; set; }
}

public sealed class RequestDefinition
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, string> Query { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int TimeoutMs { get; set; } = 30_000;
    public int MaxBodyBytes { get; set; } = 10 * 1024 * 1024;
}

public sealed class RenderDefinition
{
    public string Mode { get; set; } = "http";
    public List<string> FallbackOn { get; set; } = new();
}

public sealed class ResponseDefinition
{
    public string Type { get; set; } = "html";
}

public sealed class ExtractDefinition
{
    public SelectorDefinition Items { get; set; } = new();
    public Dictionary<string, FieldDefinition> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SelectorDefinition
{
    public string? Css { get; set; }
    public string? JsonPath { get; set; }
}

public sealed class FieldDefinition
{
    public string Source { get; set; } = "text";
    public string? Selector { get; set; }
    public string? Attribute { get; set; }
    public string? JsonPath { get; set; }
    public string? Value { get; set; }
    public string? Input { get; set; }
    public List<TransformDefinition> Transforms { get; set; } = new();
}

public sealed class TransformDefinition
{
    public string Kind { get; set; } = string.Empty;
    public string? Argument { get; set; }
    public string? Pattern { get; set; }
    public string? Replacement { get; set; }
}

public sealed class PaginationDefinition
{
    public string Strategy { get; set; } = "none";
    public string? Selector { get; set; }
    public string? Parameter { get; set; }
    public int Start { get; set; }
    public int Step { get; set; } = 1;
    public int MaxPages { get; set; } = 1;
    public string? CursorPath { get; set; }
}

public sealed class PolicyDefinition
{
    public List<string> RequiredFields { get; set; } = new();
    public string Robots { get; set; } = "report";
}

public sealed class AuthDefinition
{
    public string? LoginUrl { get; set; }
    public string? SuccessSelector { get; set; }
    public string? SessionName { get; set; }
    public List<LoginStep> Steps { get; set; } = new();
}

public sealed class LoginStep
{
    public string Kind { get; set; } = string.Empty;
    public string? Selector { get; set; }
    public string? Value { get; set; }
    public string? SecretName { get; set; }
    public int TimeoutMs { get; set; } = 30_000;
}
