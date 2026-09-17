using System.Text;
using System.Text.Json;

namespace Paytness.Scenario;

public static class JsonBindingTemplate
{
    public const int MaxBodyBytes = 256 * 1024;
    private const string BindProperty = "$bind";

    public static void Validate(JsonElement template, IReadOnlySet<string> allowedBindings)
    {
        ArgumentNullException.ThrowIfNull(allowedBindings);
        if (Encoding.UTF8.GetByteCount(template.GetRawText()) > MaxBodyBytes)
            throw new ScenarioValidationException($"JSON binding template exceeds {MaxBodyBytes} bytes.");
        ValidateElement(template, allowedBindings);
    }

    public static byte[] Render(JsonElement template, IReadOnlyDictionary<string, JsonElement> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteElement(writer, template, bindings);
        }
        if (stream.Length > MaxBodyBytes)
            throw new InvalidOperationException($"Rendered JSON binding template exceeds {MaxBodyBytes} bytes.");
        return stream.ToArray();
    }

    private static void ValidateElement(JsonElement element, IReadOnlySet<string> allowedBindings)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            bool hasBind = element.TryGetProperty(BindProperty, out JsonElement binding);
            if (hasBind)
            {
                if (element.EnumerateObject().Count() != 1 || binding.ValueKind != JsonValueKind.String)
                    throw new ScenarioValidationException("A $bind node must be an object containing exactly one string $bind property.");
                string name = binding.GetString() ?? string.Empty;
                if (!allowedBindings.Contains(name))
                    throw new ScenarioValidationException($"Unknown JSON template binding '{name}'.");
                return;
            }
            foreach (JsonProperty property in element.EnumerateObject())
                ValidateElement(property.Value, allowedBindings);
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                ValidateElement(item, allowedBindings);
        }
    }

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element, IReadOnlyDictionary<string, JsonElement> bindings)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(BindProperty, out JsonElement binding))
        {
            string name = binding.GetString() ?? throw new InvalidOperationException("Binding name is missing.");
            if (!bindings.TryGetValue(name, out JsonElement value))
                throw new InvalidOperationException($"JSON template binding '{name}' is unavailable at render time.");
            value.WriteTo(writer);
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value, bindings);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                    WriteElement(writer, item, bindings);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
