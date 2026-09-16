using System.Globalization;
using System.Text.Json;
using Paytness.Observation;
using Paytness.Provider;
using Paytness.Reporting;
using Paytness.Scenario;
using Paytness.Webhooks;

namespace Paytness.Execution;

public static class InvariantEvaluator
{
    public static IReadOnlyList<InvariantResult> Evaluate(
        IReadOnlyList<AssertionSpec> assertions,
        ProviderMetrics metrics,
        WebhookMetrics webhooks,
        IReadOnlyDictionary<string, JsonElement> observations)
    {
        var results = new List<InvariantResult>(assertions.Count);
        foreach (AssertionSpec assertion in assertions)
        {
            results.Add(EvaluateOne(assertion, metrics, webhooks, observations));
        }
        return results;
    }

    private static InvariantResult EvaluateOne(
        AssertionSpec assertion,
        ProviderMetrics metrics,
        WebhookMetrics webhooks,
        IReadOnlyDictionary<string, JsonElement> observations)
    {
        try
        {
            return assertion.Kind switch
            {
                "provider_request_count" => CompareScalar(assertion, metrics.RequestCount),
                "provider_attempt_count" => CompareScalar(assertion, metrics.AttemptCount),
                "provider_effect_count" => CompareScalar(assertion, metrics.EconomicEffectCount),
                "distinct_idempotency_key_count" => CompareScalar(assertion, metrics.DistinctIdempotencyKeyCount),
                "logical_payment_count" => CompareScalar(assertion, metrics.LogicalPaymentCount),
                "webhook_delivery_count" => CompareScalar(assertion, webhooks.DeliveryCount),
                "webhook_acked_count" => CompareScalar(assertion, webhooks.AckedCount),
                "webhook_rejected_count" => CompareScalar(assertion, webhooks.RejectedCount),
                "webhook_timeout_count" => CompareScalar(assertion, webhooks.TimedOutCount),
                "webhook_network_error_count" => CompareScalar(assertion, webhooks.NetworkErrorCount),
                "observation" => EvaluateObservation(assertion, observations),
                _ => Error(assertion, $"Unknown invariant kind '{assertion.Kind}'."),
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or OverflowException)
        {
            return Error(assertion, exception.Message);
        }
    }

    private static InvariantResult EvaluateObservation(AssertionSpec assertion, IReadOnlyDictionary<string, JsonElement> observations)
    {
        if (assertion.Observation is null || !observations.TryGetValue(assertion.Observation, out JsonElement snapshot))
            return Error(assertion, "Referenced observation was not collected.");
        if (assertion.Rfc6901 is null)
            return Error(assertion, "Observation invariant requires a JSON Pointer.");

        bool exists = JsonPointer.TryResolve(snapshot, assertion.Rfc6901, out JsonElement actual);
        if (assertion.Comparator == "exists") return Result(assertion, true, exists, exists);
        if (assertion.Comparator == "absent") return Result(assertion, true, !exists, !exists);
        if (!exists) return Result(assertion, ExpectedValue(assertion), null, pass: false, "JSON Pointer did not resolve.");

        bool pass = CompareJson(actual, assertion.Expected, assertion.Comparator);
        return Result(assertion, ExpectedValue(assertion), ToReportValue(actual), pass);
    }

    private static InvariantResult CompareScalar(AssertionSpec assertion, int actual)
    {
        if (assertion.Expected is not JsonElement expected || expected.ValueKind != JsonValueKind.Number || !expected.TryGetInt32(out int expectedInt))
            return Error(assertion, "Numeric invariant requires an integer expected value.");

        bool pass = assertion.Comparator switch
        {
            "eq" or "count_eq" => actual == expectedInt,
            "ne" => actual != expectedInt,
            "gt" => actual > expectedInt,
            "gte" => actual >= expectedInt,
            "lt" => actual < expectedInt,
            "lte" => actual <= expectedInt,
            _ => throw new InvalidOperationException($"Comparator '{assertion.Comparator}' is not valid for numeric invariants."),
        };
        return Result(assertion, expectedInt, actual, pass);
    }

    private static bool CompareJson(JsonElement actual, JsonElement? expectedValue, string comparator)
    {
        if (expectedValue is not JsonElement expected)
            throw new InvalidOperationException($"Comparator '{comparator}' requires an expected value.");

        if (comparator == "one_of")
        {
            if (expected.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("one_of expects an array.");
            return expected.EnumerateArray().Any(candidate => JsonEquals(actual, candidate));
        }

        if (comparator is "eq" or "ne")
        {
            bool equal = JsonEquals(actual, expected);
            return comparator == "eq" ? equal : !equal;
        }

        if (actual.ValueKind != JsonValueKind.Number || expected.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException($"Comparator '{comparator}' requires numbers.");

        decimal left = actual.GetDecimal();
        decimal right = expected.GetDecimal();
        return comparator switch
        {
            "gt" => left > right,
            "gte" => left >= right,
            "lt" => left < right,
            "lte" => left <= right,
            _ => throw new InvalidOperationException($"Unsupported comparator '{comparator}'."),
        };
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind) return false;
        return left.ValueKind switch
        {
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Number => decimal.TryParse(left.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal leftNumber)
                && decimal.TryParse(right.GetRawText(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal rightNumber)
                && leftNumber == rightNumber,
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal),
        };
    }

    private static object? ExpectedValue(AssertionSpec assertion) => assertion.Expected is JsonElement expected ? ToReportValue(expected) : null;

    private static object? ToReportValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out long integer) => integer,
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
        JsonValueKind.Null => null,
        _ => element.Clone(),
    };

    private static InvariantResult Result(AssertionSpec assertion, object? expected, object? actual, bool pass, string? message = null) =>
        new(assertion.Id, assertion.Kind, pass ? InvariantStatus.Pass : InvariantStatus.Fail, expected, actual, [], message);

    private static InvariantResult Error(AssertionSpec assertion, string message) =>
        new(assertion.Id, assertion.Kind, InvariantStatus.Error, ExpectedValue(assertion), null, [], message);
}
