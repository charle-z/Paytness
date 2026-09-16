using System.Text.Json;
using System.Text.Json.Serialization;

namespace Paytness.Scenario;

public sealed record ScenarioSpec
{
    public int Version { get; init; } = 1;
    public string Id { get; init; } = string.Empty;
    public int Seed { get; init; } = 1;
    public int TimeoutSeconds { get; init; } = 60;
    public string Contract { get; init; } = string.Empty;
    public ProviderBehaviorSpec Provider { get; init; } = new();
    public IReadOnlyList<SutActionSpec> Actions { get; init; } = [];
    public WebhookSpec Webhooks { get; init; } = new();
    public IReadOnlyList<ObservationSpec> Observations { get; init; } = [];
    public IReadOnlyList<AssertionSpec> Assertions { get; init; } = [];
}

public sealed record ProviderBehaviorSpec
{
    public IReadOnlyList<string> ResponseModes { get; init; } = ["deliver"];
}

public sealed record SutActionSpec
{
    public string Method { get; init; } = "POST";
    public string Path { get; init; } = string.Empty;
    public int Concurrency { get; init; } = 1;
    public JsonElement? Body { get; init; }
}

public sealed record WebhookSpec
{
    public string? Path { get; init; }
    public IReadOnlyList<PaymentEventSpec> Events { get; init; } = [];
    public IReadOnlyList<WebhookDeliverySpec> Deliveries { get; init; } = [];
}

public sealed record PaymentEventSpec
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int AttemptOrdinal { get; init; } = 1;
}

public sealed record WebhookDeliverySpec
{
    public string EventId { get; init; } = string.Empty;
    public int DelayMs { get; init; }
}

public sealed record ObservationSpec
{
    public string Id { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int TimeoutMs { get; init; } = 3_000;
    public int IntervalMs { get; init; } = 50;
}

public sealed record AssertionSpec
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Comparator { get; init; } = "eq";
    public string? Observation { get; init; }
    [JsonPropertyName("pointer")]
    public string? Rfc6901 { get; init; }
    public JsonElement? Expected { get; init; }
}

public sealed record ProviderContract
{
    public int Version { get; init; } = 1;
    public ProviderOperationContract CreatePayment { get; init; } = new();
    public WebhookContract Webhook { get; init; } = new();
    public IReadOnlyDictionary<string, string> StateValues { get; init; } = new Dictionary<string, string>();
}

public sealed record ProviderOperationContract
{
    public string Method { get; init; } = "POST";
    public string Path { get; init; } = "/provider/v1/payments";
    public string IdempotencyHeader { get; init; } = "Idempotency-Key";
    public ProviderExtractionSpec Extract { get; init; } = new();
}

public sealed record ProviderExtractionSpec
{
    public string LogicalPayment { get; init; } = "/merchantReference";
    public string? AmountMinor { get; init; } = "/amountMinor";
    public string? Currency { get; init; } = "/currency";
}

public sealed record WebhookContract
{
    public string SignatureHeader { get; init; } = "X-Paytness-Signature";
    public string EventIdProperty { get; init; } = "eventId";
    public string EventTypeProperty { get; init; } = "eventType";
    public string EventOccurredAtProperty { get; init; } = "eventOccurredAt";
    public string AttemptIdProperty { get; init; } = "providerAttemptId";
    public string LogicalPaymentProperty { get; init; } = "logicalPayment";
    public string StateProperty { get; init; } = "state";
}

public sealed record LoadedScenario(ScenarioSpec Scenario, ProviderContract Contract, string ScenarioPath, string ScenarioHash);
