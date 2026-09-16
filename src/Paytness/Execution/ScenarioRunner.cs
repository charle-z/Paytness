using System.Text;
using System.Text.Json;
using Paytness.Provider;
using Paytness.Reporting;
using Paytness.Scenario;
using Paytness.Security;
using Paytness.Webhooks;

namespace Paytness.Execution;

public static class ScenarioRunner
{
    public static async Task<RunReport> RunAsync(LoadedScenario loaded, RunOptions options, CancellationToken cancellationToken = default)
    {
        TargetPolicy.Validate(options.SutOrigin, options.AllowedTargets, options.AllowPublicTargets);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(loaded.Scenario.TimeoutSeconds));
        CancellationToken token = timeoutCts.Token;

        await using ProviderHost provider = await ProviderHost.StartAsync(
            loaded.Contract,
            loaded.Scenario.Provider.ResponseModes,
            options.ProviderEndpoint,
            token);

        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = options.SutOrigin,
            Timeout = Timeout.InfiniteTimeSpan,
        };

        using var outboundLimiter = new SemaphoreSlim(32, 32);
        foreach (SutActionSpec action in loaded.Scenario.Actions)
        {
            Task[] sends = Enumerable.Range(0, action.Concurrency)
                .Select(_ => SendSutActionAsync(client, action, outboundLimiter, token))
                .ToArray();
            await Task.WhenAll(sends);
        }

        WebhookDispatchResult webhookDispatch = await WebhookDispatcher.DispatchAsync(
            client,
            loaded.Scenario,
            loaded.Contract,
            provider.State,
            options.WebhookSecret,
            token);

        var observations = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (ObservationSpec observation in loaded.Scenario.Observations)
            observations[observation.Id] = await ObserveAsync(client, observation, token);

        ProviderMetrics metrics = provider.State.GetMetrics();
        IReadOnlyList<InvariantResult> invariants = InvariantEvaluator.Evaluate(
            loaded.Scenario.Assertions,
            metrics,
            webhookDispatch.Metrics,
            observations);
        InvariantStatus result = invariants.Any(static invariant => invariant.Status == InvariantStatus.Error)
            ? InvariantStatus.Error
            : invariants.Any(static invariant => invariant.Status == InvariantStatus.Fail)
                ? InvariantStatus.Fail
                : InvariantStatus.Pass;

        EvidenceItem[] providerEvidence = provider.State.GetRequestEvidence()
            .Select(static request => new EvidenceItem(
                request.Ordinal,
                "provider_request",
                $"request-{request.Ordinal:D4}",
                request.IdempotencyConflict
                    ? "Idempotency conflict."
                    : request.Created
                        ? "Created provider attempt and economic effect."
                        : "Reused existing provider attempt.",
                new Dictionary<string, object?>
                {
                    ["logicalPayment"] = request.LogicalPayment,
                    ["payloadFingerprint"] = request.PayloadFingerprint,
                    ["idempotencyKeyFingerprint"] = request.IdempotencyKeyFingerprint,
                    ["attemptId"] = request.AttemptId,
                    ["created"] = request.Created,
                    ["idempotencyConflict"] = request.IdempotencyConflict,
                }))
            .ToArray();
        long webhookSequenceBase = providerEvidence.Length;
        EvidenceItem[] webhookEvidence = webhookDispatch.Evidence
            .Select(delivery => new EvidenceItem(
                webhookSequenceBase + delivery.Ordinal,
                "webhook_delivery",
                delivery.DeliveryId,
                $"Webhook {delivery.Status}.",
                new Dictionary<string, object?>
                {
                    ["eventId"] = delivery.EventId,
                    ["status"] = delivery.Status.ToString(),
                    ["httpStatusCode"] = delivery.HttpStatusCode,
                }))
            .ToArray();

        EvidenceItem[] evidence = [.. providerEvidence, .. webhookEvidence];
        string[] providerEvidenceRefs = providerEvidence.Select(static item => item.EntityId).ToArray();
        string[] webhookEvidenceRefs = webhookEvidence.Select(static item => item.EntityId).ToArray();
        invariants = invariants.Select(invariant => invariant.Kind switch
        {
            "observation" => invariant,
            _ when invariant.Kind.StartsWith("webhook_", StringComparison.Ordinal) => invariant with { EvidenceRefs = webhookEvidenceRefs },
            _ => invariant with { EvidenceRefs = providerEvidenceRefs },
        }).ToArray();

        return new RunReport(
            1,
            loaded.Scenario.Id,
            loaded.Scenario.Seed,
            loaded.ScenarioHash,
            result,
            metrics,
            webhookDispatch.Metrics,
            invariants,
            evidence);
    }

    private static async Task SendSutActionAsync(
        HttpClient client,
        SutActionSpec action,
        SemaphoreSlim limiter,
        CancellationToken cancellationToken)
    {
        await limiter.WaitAsync(cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(action.Method), action.Path);
            if (action.Body is JsonElement body)
                request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"SUT action '{action.Path}' returned HTTP {(int)response.StatusCode}.");
        }
        finally
        {
            limiter.Release();
        }
    }

    private static async Task<JsonElement> ObserveAsync(HttpClient client, ObservationSpec observation, CancellationToken runToken)
    {
        using var observationCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
        observationCts.CancelAfter(TimeSpan.FromMilliseconds(observation.TimeoutMs));
        CancellationToken token = observationCts.Token;

        Exception? lastError = null;
        while (!token.IsCancellationRequested)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(observation.Path, HttpCompletionOption.ResponseHeadersRead, token);
                if (response.IsSuccessStatusCode)
                {
                    await using Stream stream = await response.Content.ReadAsStreamAsync(token);
                    using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
                    return document.RootElement.Clone();
                }

                lastError = new HttpRequestException($"Observation returned HTTP {(int)response.StatusCode}.");
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException)
            {
                lastError = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(observation.IntervalMs), token);
        }

        throw new TimeoutException(
            $"Observation '{observation.Id}' did not succeed within {observation.TimeoutMs} ms.",
            lastError);
    }
}
