namespace Paytness.Reference;

public sealed record ReferencePayRequest
{
    public string LogicalPayment { get; init; } = string.Empty;
    public long AmountMinor { get; init; } = 1_250;
    public bool RetryOnAmbiguity { get; init; } = true;
}

internal sealed record ReferenceWebhookPayload
{
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string ProviderAttemptId { get; init; } = string.Empty;
    public string LogicalPayment { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}
