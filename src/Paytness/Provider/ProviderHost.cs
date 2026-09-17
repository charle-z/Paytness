using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Paytness.Observation;
using Paytness.Scenario;

namespace Paytness.Provider;

public sealed class ProviderHost : IAsyncDisposable
{
    private readonly WebApplication _application;

    private ProviderHost(WebApplication application, ProviderState state, Uri origin)
    {
        _application = application;
        State = state;
        Origin = origin;
    }

    public ProviderState State { get; }
    public Uri Origin { get; }

    public static async Task<ProviderHost> StartAsync(
        ProviderContract contract,
        IReadOnlyList<string> responseModes,
        IPEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 256 * 1024;
            options.Listen(endpoint.Address, endpoint.Port);
        });

        WebApplication app = builder.Build();
        var state = new ProviderState();
        int responseOrdinal = -1;

        app.MapGet("/__paytness/health", static () => Results.Ok(new { status = "ok" }));
        app.MapMethods(contract.CreatePayment.Path, [contract.CreatePayment.Method], async context =>
        {
            JsonDocument payload;
            try
            {
                payload = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            }
            catch (JsonException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { error = "invalid_json" }, context.RequestAborted);
                return;
            }

            using (payload)
            {
                if (!JsonPointer.TryResolve(payload.RootElement, contract.CreatePayment.Extract.LogicalPayment, out JsonElement logicalPaymentElement))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "logical_payment_missing" }, context.RequestAborted);
                    return;
                }

                string logicalPayment = logicalPaymentElement.ValueKind == JsonValueKind.String
                    ? logicalPaymentElement.GetString() ?? string.Empty
                    : logicalPaymentElement.GetRawText();
                string? idempotencyKey = context.Request.Headers.TryGetValue(contract.CreatePayment.IdempotencyHeader, out var header)
                    ? header.ToString()
                    : null;

                ProviderProcessResult result = state.Process(payload.RootElement, logicalPayment, idempotencyKey);
                if (result.IdempotencyConflict)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await context.Response.WriteAsJsonAsync(new { error = "idempotency_key_reused" }, context.RequestAborted);
                    return;
                }

                int ordinal = Interlocked.Increment(ref responseOrdinal);
                string mode = ordinal < responseModes.Count ? responseModes[ordinal] : "deliver";
                if (string.Equals(mode, "abort_after_commit", StringComparison.Ordinal))
                {
                    context.Abort();
                    return;
                }

                string providerState = contract.StateValues.TryGetValue("succeeded", out string? mappedState) ? mappedState : "SUCCEEDED";
                context.Response.StatusCode = StatusCodes.Status200OK;
                if (contract.CreatePayment.ResponseBody is JsonElement responseTemplate)
                {
                    var bindings = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["providerAttemptId"] = JsonSerializer.SerializeToElement(result.Attempt.Id),
                        ["logicalPayment"] = JsonSerializer.SerializeToElement(result.Attempt.LogicalPayment),
                        ["providerState"] = JsonSerializer.SerializeToElement(providerState),
                        ["amountMinor"] = ExtractOrNull(payload.RootElement, contract.CreatePayment.Extract.AmountMinor),
                        ["currency"] = ExtractOrNull(payload.RootElement, contract.CreatePayment.Extract.Currency),
                    };
                    byte[] responseBody = JsonBindingTemplate.Render(responseTemplate, bindings);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength = responseBody.Length;
                    await context.Response.Body.WriteAsync(responseBody, context.RequestAborted);
                }
                else
                {
                    await context.Response.WriteAsJsonAsync(new
                    {
                        providerAttemptId = result.Attempt.Id,
                        logicalPayment = result.Attempt.LogicalPayment,
                        state = providerState,
                    }, context.RequestAborted);
                }
            }
        });

        await app.StartAsync(cancellationToken);
        return new ProviderHost(app, state, new Uri($"http://{endpoint.Address}:{endpoint.Port}"));
    }

    private static JsonElement ExtractOrNull(JsonElement payload, string? pointer)
    {
        if (pointer is not null && JsonPointer.TryResolve(payload, pointer, out JsonElement value))
            return value.Clone();
        return JsonSerializer.SerializeToElement<object?>(null);
    }

    public async ValueTask DisposeAsync()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _application.StopAsync(cancellation.Token);
        await _application.DisposeAsync();
    }
}
