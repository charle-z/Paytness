using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Paytness.Provider;
using Paytness.Scenario;
using Paytness.Tests.Support;
using Paytness.Webhooks;

namespace Paytness.Tests.Integration;

public sealed class WebhookContractTemplateTests
{
    [Fact]
    public async Task NestedWebhookTemplateIsSignedOverRenderedBytes()
    {
        const string signingKey = "test-signing-key";
        int port = TestPaths.GetFreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        WebApplication app = builder.Build();
        byte[]? capturedBody = null;
        bool signatureVerified = false;

        app.MapPost("/webhook", async context =>
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
            byte[] body = buffer.ToArray();
            byte[] digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), body);
            string expected = $"sha256={Convert.ToHexStringLower(digest)}";
            string actual = context.Request.Headers["X-Paytness-Signature"].ToString();
            signatureVerified = string.Equals(expected, actual, StringComparison.Ordinal);
            capturedBody = body;
            context.Response.StatusCode = signatureVerified ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized;
        });

        await app.StartAsync(CancellationToken.None);
        try
        {
            using JsonDocument template = JsonDocument.Parse("{\"event\":{\"id\":{\"$bind\":\"eventId\"},\"type\":{\"$bind\":\"eventType\"},\"occurredAt\":{\"$bind\":\"eventOccurredAt\"}},\"payment\":{\"attemptId\":{\"$bind\":\"providerAttemptId\"},\"reference\":{\"$bind\":\"logicalPayment\"},\"state\":{\"$bind\":\"providerState\"}}}");
            ProviderContract contract = new()
            {
                Version = 1,
                Webhook = new WebhookContract { Body = template.RootElement.Clone() },
                StateValues = new Dictionary<string, string>(StringComparer.Ordinal) { ["succeeded"] = "PAID" },
            };
            ScenarioSpec scenario = new()
            {
                Version = 1,
                Id = "nested-webhook",
                Contract = "unused.yaml",
                Webhooks = new WebhookSpec
                {
                    Path = "/webhook",
                    Events = [new PaymentEventSpec { Id = "evt-1", Type = "payment.succeeded", AttemptOrdinal = 1 }],
                    Deliveries = [new WebhookDeliverySpec { EventId = "evt-1" }],
                },
            };
            var state = new ProviderState();
            using JsonDocument requestBody = JsonDocument.Parse("{\"merchantReference\":\"order-7\",\"amountMinor\":500}");
            _ = state.Process(requestBody.RootElement, "order-7", "stable-key");
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            WebhookDispatchResult result = await WebhookDispatcher.DispatchAsync(
                client, scenario, contract, state, signingKey, CancellationToken.None);

            Assert.Equal(1, result.Metrics.AckedCount);
            Assert.True(signatureVerified);
            Assert.NotNull(capturedBody);
            using JsonDocument captured = JsonDocument.Parse(capturedBody);
            Assert.Equal("evt-1", captured.RootElement.GetProperty("event").GetProperty("id").GetString());
            Assert.Equal("payment.succeeded", captured.RootElement.GetProperty("event").GetProperty("type").GetString());
            Assert.Equal("att-0001", captured.RootElement.GetProperty("payment").GetProperty("attemptId").GetString());
            Assert.Equal("order-7", captured.RootElement.GetProperty("payment").GetProperty("reference").GetString());
            Assert.Equal("PAID", captured.RootElement.GetProperty("payment").GetProperty("state").GetString());
        }
        finally
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(stop.Token);
            await app.DisposeAsync();
        }
    }
}
