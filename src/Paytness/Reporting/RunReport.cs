using Paytness.Provider;
using Paytness.Webhooks;

namespace Paytness.Reporting;

public enum InvariantStatus
{
    Pass,
    Fail,
    Error,
}

public sealed record InvariantResult(
    string Id,
    string Kind,
    InvariantStatus Status,
    object? Expected,
    object? Actual,
    IReadOnlyList<string> EvidenceRefs,
    string? Message = null);

public sealed record EvidenceItem(
    long Sequence,
    string Category,
    string EntityId,
    string Summary,
    IReadOnlyDictionary<string, object?> Metadata);

public sealed record RunReport(
    int SchemaVersion,
    string ScenarioId,
    int Seed,
    string ScenarioHash,
    InvariantStatus Result,
    ProviderMetrics Provider,
    WebhookMetrics Webhooks,
    IReadOnlyList<InvariantResult> Invariants,
    IReadOnlyList<EvidenceItem> Evidence);
