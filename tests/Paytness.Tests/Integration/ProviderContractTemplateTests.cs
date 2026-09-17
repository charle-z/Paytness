using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Paytness.Provider;
using Paytness.Scenario;
using Paytness.Tests.Support;

namespace Paytness.Tests.Integration;

public sealed class ProviderContractTemplateTests
{
    [Fact]
    public async Task NestedResponseTemplatePreservesBoundTypes()
    {
        using JsonDocument template = JsonDocument.Parse("{\"payment\":{\"id\":{\"$bind\":\"providerAttemptId\"},\"reference\":{\"$bind\":\"logicalPayment\"},\"amount\":{\"$bind\":\"amountMinor\"},\"currency\":{\"$bind\":\"currency\"},\"status\":{\"$bind\":\"providerState\"}},\"static\":true}");
        ProviderContract contract = new()
        {
            Version = 1,
            CreatePayment = new ProviderOperationContract
            {
                ResponseBody = template.RootElement.Clone(),
            },
            StateValues = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["succeeded"] = "PAID",
            },
        };
        int port = TestPaths.GetFreePort();
        await using ProviderHost provider = await ProviderHost.StartAsync(
            contract,
            ["deliver"],
            new IPEndPoint(IPAddress.Loopback, port),
            CancellationToken.None);
        using var client = new HttpClient { BaseAddress = provider.Origin };
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/provider/v1/payments",
            new { merchantReference = "order-42", amountMinor = 1250, currency = "USD" },
            CancellationToken.None);

        response.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(CancellationToken.None));
        JsonElement payment = body.RootElement.GetProperty("payment");
        Assert.Equal("att-0001", payment.GetProperty("id").GetString());
        Assert.Equal("order-42", payment.GetProperty("reference").GetString());
        Assert.Equal(JsonValueKind.Number, payment.GetProperty("amount").ValueKind);
        Assert.Equal(1250, payment.GetProperty("amount").GetInt32());
        Assert.Equal("USD", payment.GetProperty("currency").GetString());
        Assert.Equal("PAID", payment.GetProperty("status").GetString());
        Assert.True(body.RootElement.GetProperty("static").GetBoolean());
    }
}
