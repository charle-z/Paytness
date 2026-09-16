using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Paytness.Tests.Integration;

internal sealed class SyntheticSut : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _providerClient;
    private readonly object _gate = new();
    private readonly HashSet<string> _seenEventIds = new(StringComparer.Ordinal);
    private readonly List<string> _webhookEventTypes = [];
    private readonly string? _webhookSecret;
    private string _paymentStatus = "Pending";
    private int _orderPaidEventCount;
    private int _webhookReceivedCount;
    private int _webhookAppliedCount;
    private int _webhookSignatureFailureCount;

    private SyntheticSut(WebApplication app, Uri origin, HttpClient providerClient, string? webhookSecret)
    {
        _app = app;
        Origin = origin;
        _providerClient = providerClient;
        _webhookSecret = webhookSecret;
    }

    public Uri Origin { get; }

    public static Task<SyntheticSut> StartAsync(
        Uri providerOrigin,
        bool unstableRetry,
        CancellationToken cancellationToken = default) =>
        StartAsync(providerOrigin, unstableRetry, null, cancellationToken);

    public static async Task<SyntheticSut> StartAsync(
        Uri providerOrigin,
        bool unstableRetry,
        string? webhookSecret,
        CancellationToken cancellationToken = default)
    {
        int port = GetFreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        WebApplication app = builder.Build();
        var providerClient = new HttpClient { BaseAddress = providerOrigin, Timeout = TimeSpan.FromSeconds(3) };
        var sut = new SyntheticSut(app, new Uri($"http://127.0.0.1:{port}"), providerClient, webhookSecret);

        app.MapPost("/test/pay", async context =>
        {
            const string stableKey = "order-123";
            HttpResponseMessage? response;
            try
            {
                response = await sut.SendPaymentAsync(stableKey, context.RequestAborted);
            }
            catch (HttpRequestException)
            {
                response = await sut.SendPaymentAsync(unstableRetry ? "order-123-retry" : stableKey, context.RequestAborted);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    return;
                }
            }

            lock (sut._gate)
            {
                sut.ApplyPaidEffect();
            }
            await context.Response.WriteAsJsonAsync(new { status = "ok" }, context.RequestAborted);
        });

        app.MapPost("/test/webhook", async context =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            byte[] body = buffer.ToArray();
            string signature = context.Request.Headers["X-Paytness-Signature"].ToString();
            if (!sut.ValidateSignature(body, signature))
            {
                lock (sut._gate) sut._webhookSignatureFailureCount++;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using JsonDocument document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("eventId", out JsonElement eventIdElement) ||
                !document.RootElement.TryGetProperty("eventType", out JsonElement eventTypeElement))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            string eventId = eventIdElement.GetString() ?? string.Empty;
            string eventType = eventTypeElement.GetString() ?? string.Empty;
            lock (sut._gate)
            {
                sut._webhookReceivedCount++;
                sut._webhookEventTypes.Add(eventType);
                if (sut._seenEventIds.Add(eventId))
                {
                    sut._webhookAppliedCount++;
                    if (string.Equals(eventType, "payment.succeeded", StringComparison.Ordinal))
                        sut.ApplyPaidEffect();
                }
            }

            await context.Response.WriteAsJsonAsync(new { status = "accepted" }, context.RequestAborted);
        });

        app.MapGet("/test/state", () =>
        {
            lock (sut._gate)
            {
                return Results.Ok(new
                {
                    paymentStatus = sut._paymentStatus,
                    orderPaidEventCount = sut._orderPaidEventCount,
                    webhookReceivedCount = sut._webhookReceivedCount,
                    webhookAppliedCount = sut._webhookAppliedCount,
                    webhookSignatureFailureCount = sut._webhookSignatureFailureCount,
                    webhookEventTypes = sut._webhookEventTypes.ToArray(),
                });
            }
        });

        await app.StartAsync(cancellationToken);
        return sut;
    }

    private void ApplyPaidEffect()
    {
        if (string.Equals(_paymentStatus, "Paid", StringComparison.Ordinal)) return;
        _paymentStatus = "Paid";
        _orderPaidEventCount++;
    }

    private bool ValidateSignature(byte[] body, string signature)
    {
        if (string.IsNullOrEmpty(_webhookSecret) || string.IsNullOrEmpty(signature)) return false;
        byte[] digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_webhookSecret), body);
        string expected = $"sha256={Convert.ToHexStringLower(digest)}";
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        byte[] actualBytes = Encoding.ASCII.GetBytes(signature);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private async Task<HttpResponseMessage> SendPaymentAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var payload = new { merchantReference = "order-123", amountMinor = 1250, currency = "USD" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/provider/v1/payments")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await _providerClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        _providerClient.Dispose();
        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _app.StopAsync(stopCts.Token);
        await _app.DisposeAsync();
    }
}
