using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Paytness.Scenario;

public static class DocumentStructuralValidator
{
    public const int MaxDepth = 32;
    public const int MaxNodes = 10_000;
    public const int MaxScalarBytes = 256 * 1024;

    public static void ValidateYaml(string text)
    {
        var parser = new Parser(new StringReader(text));
        int eventCount = 0;
        int depth = 0;
        while (parser.MoveNext())
        {
            ParsingEvent? current = parser.Current;
            if (++eventCount > MaxNodes)
                throw new ScenarioValidationException($"YAML exceeds {MaxNodes} parsing events.");
            if (current is AnchorAlias)
                throw new ScenarioValidationException("YAML aliases are not allowed.");
            if (current is NodeEvent node)
            {
                if (!node.Anchor.IsEmpty) throw new ScenarioValidationException("YAML anchors are not allowed.");
                if (node.Tag.IsLocal) throw new ScenarioValidationException("Custom YAML tags are not allowed.");
            }

            if (current is MappingStart or SequenceStart)
            {
                if (++depth > MaxDepth) throw new ScenarioValidationException($"YAML nesting exceeds depth {MaxDepth}.");
            }
            else if (current is MappingEnd or SequenceEnd)
            {
                depth--;
            }
            else if (current is Scalar scalar)
            {
                if (Encoding.UTF8.GetByteCount(scalar.Value) > MaxScalarBytes)
                    throw new ScenarioValidationException($"YAML scalar exceeds {MaxScalarBytes} bytes.");
                if (string.Equals(scalar.Value, "<<", StringComparison.Ordinal))
                    throw new ScenarioValidationException("YAML merge keys are not allowed.");
            }
        }
    }

    public static void ValidateJson(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaxDepth,
        });
        int nodes = 0;
        ValidateJsonElement(document.RootElement, ref nodes);
    }

    private static void ValidateJsonElement(JsonElement element, ref int nodes)
    {
        if (++nodes > MaxNodes) throw new ScenarioValidationException($"JSON exceeds {MaxNodes} nodes.");
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (!names.Add(property.Name))
                            throw new ScenarioValidationException($"JSON contains duplicate property '{property.Name}'.");
                        if (Encoding.UTF8.GetByteCount(property.Name) > MaxScalarBytes)
                            throw new ScenarioValidationException($"JSON property name exceeds {MaxScalarBytes} bytes.");
                        ValidateJsonElement(property.Value, ref nodes);
                    }
                    break;
                }
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray()) ValidateJsonElement(item, ref nodes);
                break;
            case JsonValueKind.String:
                if (Encoding.UTF8.GetByteCount(element.GetString() ?? string.Empty) > MaxScalarBytes)
                    throw new ScenarioValidationException($"JSON scalar exceeds {MaxScalarBytes} bytes.");
                break;
        }
    }
}
