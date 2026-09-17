using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paytness.Execution;
using Paytness.Provider;
using Paytness.Scenario;

namespace Paytness.Webhooks;

public sealed class WebhookDispatchException(string message) : Exception(message);

public static class WebhookDispatcher
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);

    public static async Task<WebhookDispatchResult> DispatchAsync(
        HttpClient client,
        ScenarioSpec scenario,
        ProviderContract contract,
        ProviderState providerState,
        string? secret,
        CancellationToken cancellationToken)
    {
        if (scenario.Webhooks.Deliveries.Count == 0)
            return new WebhookDispatchResult(WebhookMetrics.Empty, []);
        if (string.IsNullOrEmpty(secret))
            throw new WebhookDispatchException("Webhook deliveries require a secret supplied by the operator.");
        if (string.IsNullOrWhiteSpace(scenario.Webhooks.Path))
            throw new WebhookDispatchException("Webhook path is missing.");

        IReadOnlyList<ProviderAttempt> attempts = providerState.GetAttempts();
        Dictionary<string, PaymentEvent> events = BuildEvents(scenario.Webhooks.Events, contract, attempts);
        var evidence = new List<WebhookDeliveryEvidence>(scenario.Webhooks.Deliveries.Count);

        int executionOrdinal = 0;
        var scheduledActions = new List<ScheduledAction>(scenario.Webhooks.Deliveries.Count);
        for (int index = 0; index < scenario.Webhooks.Deliveries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WebhookDeliverySpec delivery = scenario.Webhooks.Deliveries[index];
            if (!events.TryGetValue(delivery.EventId, out PaymentEvent? paymentEvent))
                throw new WebhookDispatchException($"Webhook delivery references unavailable event '{delivery.EventId}'.");

            int plannedOrdinal = index + 1;
            string deliveryId = $"delivery-{plannedOrdinal:D4}";
            byte[] body = SerializeEvent(paymentEvent, contract.Webhook);
            string signature = Sign(body, secret);
            scheduledActions.Add(new ScheduledAction(
                deliveryId,
                plannedOrdinal,
                TimeSpan.FromMilliseconds(delivery.DelayMs),
                async runToken =>
                {
                    WebhookDeliveryEvidence delivered = await DeliverAsync(
                        client,
                        scenario.Webhooks.Path,
                        contract.Webhook.SignatureHeader,
                        signature,
                        body,
                        plannedOrdinal,
                        deliveryId,
                        paymentEvent.EventId,
                        runToken);
                    evidence.Add(delivered with { Ordinal = ++executionOrdinal });
                }));
        }

        await ScheduledActionScheduler.ExecuteAsync(scheduledActions, TimeProvider.System, cancellationToken);
        return new WebhookDispatchResult(ToMetrics(evidence), evidence);
    }

    private static Dictionary<string, PaymentEvent> BuildEvents(
        IReadOnlyList<PaymentEventSpec> specs,
        ProviderContract contract,
        IReadOnlyList<ProviderAttempt> attempts)
    {
        var events = new Dictionary<string, PaymentEvent>(StringComparer.Ordinal);
        foreach (PaymentEventSpec spec in specs)
        {
            int attemptIndex = spec.AttemptOrdinal - 1;
            if (attemptIndex < 0 || attemptIndex >= attempts.Count)
                throw new WebhookDispatchException($"Payment event '{spec.Id}' references provider attempt {spec.AttemptOrdinal}, but only {attempts.Count} attempt(s) exist.");

            ProviderAttempt attempt = attempts[attemptIndex];
            string providerState = spec.Type switch
            {
                "payment.processing" => MappedState(contract, "processing", "PROCESSING"),
                "payment.succeeded" => MappedState(contract, "succeeded", "SUCCEEDED"),
                "payment.failed" => MappedState(contract, "failed", "FAILED"),
                _ => throw new WebhookDispatchException($"Unsupported event type '{spec.Type}'."),
            };

            events.Add(spec.Id, new PaymentEvent(
                spec.Id,
                spec.Type,
                DateTimeOffset.UnixEpoch.AddMilliseconds(events.Count + 1),
                attempt.Id,
                attempt.LogicalPayment,
                providerState));
        }
        return events;
    }

    private static string MappedState(ProviderContract contract, string name, string fallback) =>
        contract.StateValues.TryGetValue(name, out string? mapped) ? mapped : fallback;

    private static byte[] SerializeEvent(PaymentEvent paymentEvent, WebhookContract contract)
    {
        if (contract.Body is JsonElement template)
        {
            var bindings = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["eventId"] = JsonSerializer.SerializeToElement(paymentEvent.EventId),
                ["eventType"] = JsonSerializer.SerializeToElement(paymentEvent.EventType),
                ["eventOccurredAt"] = JsonSerializer.SerializeToElement(paymentEvent.OccurredAt),
                ["providerAttemptId"] = JsonSerializer.SerializeToElement(paymentEvent.ProviderAttemptId),
                ["logicalPayment"] = JsonSerializer.SerializeToElement(paymentEvent.LogicalPayment),
                ["providerState"] = JsonSerializer.SerializeToElement(paymentEvent.ProviderState),
            };
            return JsonBindingTemplate.Render(template, bindings);
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [contract.EventIdProperty] = paymentEvent.EventId,
            [contract.EventTypeProperty] = paymentEvent.EventType,
            [contract.EventOccurredAtProperty] = paymentEvent.OccurredAt,
            [contract.AttemptIdProperty] = paymentEvent.ProviderAttemptId,
            [contract.LogicalPaymentProperty] = paymentEvent.LogicalPayment,
            [contract.StateProperty] = paymentEvent.ProviderState,
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    private static string Sign(byte[] body, string secret)
    {
        byte[] key = Encoding.UTF8.GetBytes(secret);
        byte[] digest = HMACSHA256.HashData(key, body);
        return $"sha256={Convert.ToHexStringLower(digest)}";
    }

    private static async Task<WebhookDeliveryEvidence> DeliverAsync(
        HttpClient client,
        string path,
        string signatureHeader,
        string signature,
        byte[] body,
        int ordinal,
        string deliveryId,
        string eventId,
        CancellationToken runToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.TryAddWithoutValidation(signatureHeader, signature);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        timeoutCts.CancelAfter(DeliveryTimeout);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            WebhookDeliveryStatus status = response.IsSuccessStatusCode ? WebhookDeliveryStatus.Acked : WebhookDeliveryStatus.Rejected;
            return new(ordinal, deliveryId, eventId, status, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!runToken.IsCancellationRequested)
        {
            return new(ordinal, deliveryId, eventId, WebhookDeliveryStatus.TimedOut, null);
        }
        catch (HttpRequestException)
        {
            return new(ordinal, deliveryId, eventId, WebhookDeliveryStatus.NetworkError, null);
        }
    }

    private static WebhookMetrics ToMetrics(List<WebhookDeliveryEvidence> evidence) => new(
        evidence.Count,
        evidence.Count(static item => item.Status == WebhookDeliveryStatus.Acked),
        evidence.Count(static item => item.Status == WebhookDeliveryStatus.Rejected),
        evidence.Count(static item => item.Status == WebhookDeliveryStatus.TimedOut),
        evidence.Count(static item => item.Status == WebhookDeliveryStatus.NetworkError),
        evidence.Count(static item => item.Status == WebhookDeliveryStatus.Cancelled));
}
