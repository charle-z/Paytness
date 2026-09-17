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

internal sealed class AlternateContractSut : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _providerClient;
    private readonly object _gate = new();
    private readonly HashSet<string> _seenEvents = new(StringComparer.Ordinal);
    private readonly string _signingKey;
    private bool _responseValidated;
    private string _paymentStatus = "Pending";
    private int _webhookReceivedCount;

    private AlternateContractSut(WebApplication app, Uri origin, HttpClient providerClient, string signingKey)
    {
        _app = app;
        Origin = origin;
        _providerClient = providerClient;
        _signingKey = signingKey;
    }

    public Uri Origin { get; }

    public static async Task<AlternateContractSut> StartAsync(Uri providerOrigin, string signingKey)
    {
        int port = GetFreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        WebApplication app = builder.Build();
        var providerClient = new HttpClient { BaseAddress = providerOrigin, Timeout = TimeSpan.FromSeconds(3) };
        var sut = new AlternateContractSut(app, new Uri($"http://127.0.0.1:{port}"), providerClient, signingKey);

        app.MapPost("/test/pay", async context =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/gateway/v2/charges")
            {
                Content = JsonContent.Create(new
                {
                    order = new { reference = "alt-order-1" },
                    money = new { minor = 2468, currency = "USD" },
                }),
            };
            request.Headers.TryAddWithoutValidation("X-Idempotency-Key", "alt-order-1");
            using HttpResponseMessage response = await sut._providerClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(context.RequestAborted));
            JsonElement charge = body.RootElement.GetProperty("data").GetProperty("charge");
            bool valid =
                charge.GetProperty("id").GetString()?.StartsWith("att-", StringComparison.Ordinal) == true &&
                charge.GetProperty("reference").GetString() == "alt-order-1" &&
                charge.GetProperty("amountMinor").ValueKind == JsonValueKind.Number &&
                charge.GetProperty("amountMinor").GetInt32() == 2468 &&
                charge.GetProperty("currency").GetString() == "USD" &&
                charge.GetProperty("status").GetString() == "PAID";
            lock (sut._gate) sut._responseValidated = valid;
            context.Response.StatusCode = valid ? StatusCodes.Status200OK : StatusCodes.Status502BadGateway;
        });

        app.MapPost("/test/webhook", async context =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            byte[] body = buffer.ToArray();
            byte[] digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(sut._signingKey), body);
            string expected = $"sha256={Convert.ToHexStringLower(digest)}";
            string actual = context.Request.Headers["X-Gateway-Signature"].ToString();
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement eventNode = document.RootElement.GetProperty("event");
            JsonElement paymentNode = document.RootElement.GetProperty("payment");
            string eventId = eventNode.GetProperty("id").GetString() ?? string.Empty;
            string eventType = eventNode.GetProperty("type").GetString() ?? string.Empty;
            string state = paymentNode.GetProperty("status").GetString() ?? string.Empty;
            lock (sut._gate)
            {
                sut._webhookReceivedCount++;
                if (sut._seenEvents.Add(eventId) && eventType == "payment.succeeded" && state == "PAID")
                    sut._paymentStatus = "Paid";
            }
            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        app.MapGet("/test/state", () =>
        {
            lock (sut._gate)
                return Results.Ok(new { responseValidated = sut._responseValidated, paymentStatus = sut._paymentStatus, webhookReceivedCount = sut._webhookReceivedCount });
        });

        await app.StartAsync(CancellationToken.None);
        return sut;
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
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _app.StopAsync(stop.Token);
        await _app.DisposeAsync();
    }
}
