using System.Text.Json;
using Paytness.Scenario;

namespace Paytness.Tests.Scenario;

public sealed class JsonBindingTemplateTests
{
    [Fact]
    public void UnknownAndMalformedBindingsAreRejected()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "known" };
        using JsonDocument unknown = JsonDocument.Parse("{\"value\":{\"$bind\":\"unknown\"}}");
        using JsonDocument malformed = JsonDocument.Parse("{\"$bind\":\"known\",\"extra\":true}");

        Assert.Throws<ScenarioValidationException>(() => JsonBindingTemplate.Validate(unknown.RootElement, allowed));
        Assert.Throws<ScenarioValidationException>(() => JsonBindingTemplate.Validate(malformed.RootElement, allowed));
    }

    [Fact]
    public void RenderPreservesStaticStructureAndBoundJsonTypes()
    {
        using JsonDocument template = JsonDocument.Parse("{\"payment\":{\"id\":{\"$bind\":\"id\"},\"amount\":{\"$bind\":\"amount\"}},\"active\":true}");
        using JsonDocument amount = JsonDocument.Parse("1250");
        var bindings = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["id"] = JsonSerializer.SerializeToElement("att-0001"),
            ["amount"] = amount.RootElement.Clone(),
        };

        byte[] rendered = JsonBindingTemplate.Render(template.RootElement, bindings);
        using JsonDocument result = JsonDocument.Parse(rendered);

        Assert.Equal("att-0001", result.RootElement.GetProperty("payment").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Number, result.RootElement.GetProperty("payment").GetProperty("amount").ValueKind);
        Assert.Equal(1250, result.RootElement.GetProperty("payment").GetProperty("amount").GetInt32());
        Assert.True(result.RootElement.GetProperty("active").GetBoolean());
    }
    [Fact]
    public void TemplateAndRenderedBodiesRespectTheHardByteLimit()
    {
        string largeValue = new('x', JsonBindingTemplate.MaxBodyBytes);
        using JsonDocument oversizedTemplate = JsonDocument.Parse(JsonSerializer.Serialize(new { value = largeValue }));
        Assert.Throws<ScenarioValidationException>(() => JsonBindingTemplate.Validate(oversizedTemplate.RootElement, new HashSet<string>(StringComparer.Ordinal)));

        using JsonDocument bindingTemplate = JsonDocument.Parse("{\"value\":{\"$bind\":\"large\"}}");
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "large" };
        JsonBindingTemplate.Validate(bindingTemplate.RootElement, allowed);
        var bindings = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["large"] = JsonSerializer.SerializeToElement(largeValue),
        };
        Assert.Throws<InvalidOperationException>(() => JsonBindingTemplate.Render(bindingTemplate.RootElement, bindings));
    }

}
