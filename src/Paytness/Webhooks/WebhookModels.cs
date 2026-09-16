namespace Paytness.Webhooks;

public enum WebhookDeliveryStatus
{
    Planned,
    InFlight,
    Acked,
    Rejected,
    TimedOut,
    NetworkError,
    Cancelled,
}

public sealed record PaymentEvent(
    string EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    string ProviderAttemptId,
    string LogicalPayment,
    string ProviderState);

public sealed record WebhookDeliveryEvidence(
    int Ordinal,
    string DeliveryId,
    string EventId,
    WebhookDeliveryStatus Status,
    int? HttpStatusCode);

public sealed record WebhookMetrics(
    int DeliveryCount,
    int AckedCount,
    int RejectedCount,
    int TimedOutCount,
    int NetworkErrorCount,
    int CancelledCount)
{
    public static WebhookMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0);
}

public sealed record WebhookDispatchResult(
    WebhookMetrics Metrics,
    IReadOnlyList<WebhookDeliveryEvidence> Evidence);
