using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paytness.Domain;

namespace Paytness.Provider;

public enum ProviderAttemptStatus
{
    Created,
    Processing,
    Succeeded,
    Failed,
}

public sealed record ProviderAttempt(
    string Id,
    string LogicalPayment,
    string PayloadFingerprint,
    string? IdempotencyKeyFingerprint,
    ProviderAttemptStatus Status);

public sealed record ProviderProcessResult(ProviderAttempt Attempt, bool Created, bool IdempotencyConflict);

public sealed record ProviderMetrics(
    int LogicalPaymentCount,
    int RequestCount,
    int AttemptCount,
    int EconomicEffectCount,
    int DistinctIdempotencyKeyCount);

public sealed record ProviderRequestEvidence(
    int Ordinal,
    string LogicalPayment,
    string PayloadFingerprint,
    string? IdempotencyKeyFingerprint,
    string AttemptId,
    bool Created,
    bool IdempotencyConflict);

public sealed class ProviderState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IdempotencyEntry> _idempotency = new(StringComparer.Ordinal);
    private readonly HashSet<string> _logicalPayments = new(StringComparer.Ordinal);
    private readonly HashSet<string> _distinctKeyFingerprints = new(StringComparer.Ordinal);
    private readonly List<ProviderAttempt> _attempts = [];
    private readonly List<ProviderRequestEvidence> _requests = [];
    private int _economicEffects;

    public ProviderProcessResult Process(JsonElement payload, string logicalPayment, string? idempotencyKey)
    {
        string payloadFingerprint = JsonFingerprint.Compute(payload);
        string? keyFingerprint = string.IsNullOrEmpty(idempotencyKey) ? null : FingerprintText(idempotencyKey);

        lock (_gate)
        {
            _logicalPayments.Add(logicalPayment);
            if (keyFingerprint is not null) _distinctKeyFingerprints.Add(keyFingerprint);

            int requestOrdinal = _requests.Count + 1;
            if (keyFingerprint is not null && _idempotency.TryGetValue(keyFingerprint, out IdempotencyEntry? existing))
            {
                bool conflict = !string.Equals(existing.PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal);
                var reused = new ProviderProcessResult(existing.Attempt, Created: false, IdempotencyConflict: conflict);
                _requests.Add(ToEvidence(requestOrdinal, logicalPayment, payloadFingerprint, keyFingerprint, reused));
                return reused;
            }

            string attemptId = $"att-{_attempts.Count + 1:D4}";
            var attempt = new ProviderAttempt(attemptId, logicalPayment, payloadFingerprint, keyFingerprint, ProviderAttemptStatus.Succeeded);
            _attempts.Add(attempt);
            _economicEffects++;

            if (keyFingerprint is not null)
            {
                _idempotency.Add(keyFingerprint, new IdempotencyEntry(payloadFingerprint, attempt));
            }

            var created = new ProviderProcessResult(attempt, Created: true, IdempotencyConflict: false);
            _requests.Add(ToEvidence(requestOrdinal, logicalPayment, payloadFingerprint, keyFingerprint, created));
            return created;
        }
    }

    public ProviderMetrics GetMetrics()
    {
        lock (_gate)
        {
            return new ProviderMetrics(_logicalPayments.Count, _requests.Count, _attempts.Count, _economicEffects, _distinctKeyFingerprints.Count);
        }
    }

    public IReadOnlyList<ProviderRequestEvidence> GetRequestEvidence()
    {
        lock (_gate)
        {
            return [.. _requests];
        }
    }

    public IReadOnlyList<ProviderAttempt> GetAttempts()
    {
        lock (_gate)
        {
            return [.. _attempts];
        }
    }

    private static ProviderRequestEvidence ToEvidence(
        int ordinal,
        string logicalPayment,
        string payloadFingerprint,
        string? keyFingerprint,
        ProviderProcessResult result) =>
        new(ordinal, logicalPayment, payloadFingerprint, keyFingerprint, result.Attempt.Id, result.Created, result.IdempotencyConflict);

    private static string FingerprintText(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record IdempotencyEntry(string PayloadFingerprint, ProviderAttempt Attempt);
}
