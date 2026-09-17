using System.Text.Json;
using Paytness.Execution;
using Paytness.Provider;
using Paytness.Reporting;
using Paytness.Scenario;
using Paytness.Webhooks;

namespace Paytness.Tests.Execution;

public sealed class InvariantEvaluatorTests
{
    [Fact]
    public void ObservationEqualityDoesNotCoerceStringToNumber()
    {
        using JsonDocument expectedDocument = JsonDocument.Parse("\"1\"");
        using JsonDocument observationDocument = JsonDocument.Parse("{\"value\":1}");
        var assertion = new AssertionSpec
        {
            Id = "strict-type",
            Kind = "observation",
            Observation = "state",
            Rfc6901 = "/value",
            Comparator = "eq",
            Expected = expectedDocument.RootElement.Clone(),
        };
        var observations = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["state"] = observationDocument.RootElement.Clone(),
        };

        InvariantResult result = Assert.Single(InvariantEvaluator.Evaluate(
            [assertion],
            new ProviderMetrics(0, 0, 0, 0, 0),
            WebhookMetrics.Empty,
            observations));

        Assert.Equal(InvariantStatus.Fail, result.Status);
        Assert.Equal("1", result.Expected);
        Assert.Equal(1L, result.Actual);
    }
}
