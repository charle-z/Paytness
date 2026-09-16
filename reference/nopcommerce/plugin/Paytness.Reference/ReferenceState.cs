using System.Collections.Concurrent;

namespace Paytness.Reference;

public sealed class ReferenceState
{
    private readonly ConcurrentDictionary<Guid, PaymentState> _payments = new();
    private string? _currentLogicalPayment;

    public PaymentState Get(Guid logicalPayment) => _payments.GetOrAdd(logicalPayment, static _ => new PaymentState());

    public void SetCurrent(Guid logicalPayment) => Volatile.Write(ref _currentLogicalPayment, logicalPayment.ToString("N"));

    public bool TryGetCurrent(out Guid logicalPayment)
    {
        string? value = Volatile.Read(ref _currentLogicalPayment);
        return Guid.TryParseExact(value, "N", out logicalPayment);
    }

    public sealed class PaymentState
    {
        private readonly object _gate = new();
        private readonly HashSet<string> _appliedWebhookEvents = new(StringComparer.Ordinal);
        private readonly List<string> _webhookEventTypes = [];
        private int _webhookReceivedCount;
        private int _webhookAppliedCount;
        private int _webhookSignatureFailureCount;
        private int _orderPaidEventCount;

        public SemaphoreSlim OrderCreationGate { get; } = new(1, 1);
        public SemaphoreSlim PaymentApplicationGate { get; } = new(1, 1);

        public void RecordWebhookReceived(string eventType)
        {
            lock (_gate)
            {
                _webhookReceivedCount++;
                _webhookEventTypes.Add(eventType);
            }
        }

        public void RecordSignatureFailure()
        {
            lock (_gate) _webhookSignatureFailureCount++;
        }

        public bool TryRecordWebhookApplied(string eventId)
        {
            lock (_gate)
            {
                if (!_appliedWebhookEvents.Add(eventId)) return false;
                _webhookAppliedCount++;
                return true;
            }
        }

        public void RecordOrderPaid()
        {
            lock (_gate) _orderPaidEventCount++;
        }

        public ReferenceSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new ReferenceSnapshot(
                    _webhookReceivedCount,
                    _webhookAppliedCount,
                    _webhookSignatureFailureCount,
                    _orderPaidEventCount,
                    [.. _webhookEventTypes]);
            }
        }
    }
}

public sealed record ReferenceSnapshot(
    int WebhookReceivedCount,
    int WebhookAppliedCount,
    int WebhookSignatureFailureCount,
    int OrderPaidEventCount,
    IReadOnlyList<string> WebhookEventTypes);
