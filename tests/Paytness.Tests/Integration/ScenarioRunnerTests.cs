using System.Net;
using Paytness.Execution;
using Paytness.Reporting;
using Paytness.Scenario;
using Paytness.Tests.Support;

namespace Paytness.Tests.Integration;

public sealed class ScenarioRunnerTests
{
    [Fact]
    public async Task HealthyExternalPaymentPasses()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("healthy.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(new Uri($"http://127.0.0.1:{providerPort}"), false, token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(new Uri($"http://localhost:{sut.Origin.Port}"), new IPEndPoint(IPAddress.Loopback, providerPort), [], false),
            token);
        Assert.True(report.Result == InvariantStatus.Pass, string.Join(" | ", report.Invariants.Select(i => $"{i.Id}:{i.Status}:{i.Message}")));
        Assert.Equal(1, report.Provider.RequestCount);
        Assert.Equal(1, report.Provider.AttemptCount);
        Assert.Equal(1, report.Provider.EconomicEffectCount);
        Assert.All(report.Invariants, invariant => Assert.Equal(InvariantStatus.Pass, invariant.Status));
    }

    [Fact]
    public async Task LostResponseWithStableKeyReusesAttemptAndPasses()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("retry-after-lost-response.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(new Uri($"http://127.0.0.1:{providerPort}"), false, token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false),
            token);
        Assert.True(report.Result == InvariantStatus.Pass, string.Join(" | ", report.Invariants.Select(i => $"{i.Id}:{i.Status}:{i.Message}")));
        Assert.True(report.Provider.RequestCount >= 2);
        Assert.Equal(1, report.Provider.AttemptCount);
        Assert.Equal(1, report.Provider.EconomicEffectCount);
        Assert.Equal(1, report.Provider.DistinctIdempotencyKeyCount);
    }

    [Fact]
    public async Task LostResponseWithUnstableKeyExposesDoubleEffectAndFails()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("retry-after-lost-response.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(new Uri($"http://127.0.0.1:{providerPort}"), true, token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false),
            token);
        Assert.True(report.Result == InvariantStatus.Fail, string.Join(" | ", report.Invariants.Select(i => $"{i.Id}:{i.Status}:{i.Message}")));
        Assert.True(report.Provider.RequestCount >= 2);
        Assert.Equal(2, report.Provider.AttemptCount);
        Assert.Equal(2, report.Provider.EconomicEffectCount);
        Assert.Equal(2, report.Provider.DistinctIdempotencyKeyCount);
        Assert.Contains(report.Invariants, invariant => invariant.Id == "one-economic-effect" && invariant.Status == InvariantStatus.Fail);
        InvariantResult failedInvariant = Assert.Single(report.Invariants, invariant => invariant.Id == "one-economic-effect");
        Assert.Equal(["request-0001", "request-0002"], failedInvariant.EvidenceRefs);
    }

    [Fact]
    public async Task DuplicateWebhookUsesSameEventAndAppliesOnce()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string webhookKey = "fixture-key-accepted";
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("duplicate-webhook.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"),
            false,
            webhookKey,
            token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false, webhookKey),
            token);

        Assert.Equal(InvariantStatus.Pass, report.Result);
        Assert.Equal(2, report.Webhooks.DeliveryCount);
        Assert.Equal(2, report.Webhooks.AckedCount);
        Assert.Equal(0, report.Webhooks.RejectedCount);
        EvidenceItem[] deliveries = report.Evidence.Where(static item => item.Category == "webhook_delivery").ToArray();
        Assert.Equal(2, deliveries.Length);
        Assert.Equal("delivery-0001", deliveries[0].EntityId);
        Assert.Equal("delivery-0002", deliveries[1].EntityId);
        Assert.Equal(deliveries[0].Metadata["eventId"], deliveries[1].Metadata["eventId"]);
    }

    [Fact]
    public async Task InvalidWebhookSignatureIsRejectedAndFailsScenario()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("duplicate-webhook.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"),
            false,
            "fixture-key-sut",
            token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false, "fixture-key-runner"),
            token);

        Assert.Equal(InvariantStatus.Fail, report.Result);
        Assert.Equal(2, report.Webhooks.DeliveryCount);
        Assert.Equal(0, report.Webhooks.AckedCount);
        Assert.Equal(2, report.Webhooks.RejectedCount);
        Assert.Contains(report.Invariants, static invariant => invariant.Id == "two-webhook-acks" && invariant.Status == InvariantStatus.Fail);
    }

    [Fact]
    public async Task ScheduledWebhookDeliveryCanBeOutOfOrderAndStillConverge()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        const string webhookSecret = "fixture-out-of-order";
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("out-of-order-webhook.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"),
            false,
            webhookSecret,
            token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false, webhookSecret),
            token);

        Assert.Equal(InvariantStatus.Pass, report.Result);
        EvidenceItem[] deliveries = report.Evidence.Where(static item => item.Category == "webhook_delivery").ToArray();
        Assert.Equal(2, deliveries.Length);
        Assert.Equal("delivery-0002", deliveries[0].EntityId);
        Assert.Equal("evt-succeeded-1", deliveries[0].Metadata["eventId"]);
        Assert.Equal("delivery-0001", deliveries[1].EntityId);
        Assert.Equal("evt-processing-1", deliveries[1].Metadata["eventId"]);
    }

    [Fact]
    public async Task ConcurrentTriggersReuseOneProviderAttemptAndOneEffect()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("concurrent-same-payment.yaml"), token);
        int providerPort = TestPaths.GetFreePort();
        await using SyntheticSut sut = await SyntheticSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"),
            false,
            token);
        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false),
            token);

        Assert.Equal(InvariantStatus.Pass, report.Result);
        Assert.Equal(2, report.Provider.RequestCount);
        Assert.Equal(1, report.Provider.AttemptCount);
        Assert.Equal(1, report.Provider.EconomicEffectCount);
        Assert.Equal(1, report.Provider.DistinctIdempotencyKeyCount);
    }
    [Fact]
    public async Task AlternateProviderShapePassesWithoutScriptsOrCustomRunnerCode()
    {
        const string signingKey = "alternate-test-signing-key";
        LoadedScenario loaded = await ScenarioLoader.LoadAsync(TestPaths.Scenario("alternate-provider-shape.yaml"), CancellationToken.None);
        int providerPort = TestPaths.GetFreePort();
        await using AlternateContractSut sut = await AlternateContractSut.StartAsync(
            new Uri($"http://127.0.0.1:{providerPort}"), signingKey);

        RunReport report = await ScenarioRunner.RunAsync(
            loaded,
            new RunOptions(sut.Origin, new IPEndPoint(IPAddress.Loopback, providerPort), [], false, signingKey),
            CancellationToken.None);

        Assert.Equal(InvariantStatus.Pass, report.Result);
        Assert.Equal(1, report.Provider.RequestCount);
        Assert.Equal(1, report.Provider.AttemptCount);
        Assert.Equal(1, report.Provider.EconomicEffectCount);
        Assert.Equal(1, report.Webhooks.AckedCount);
        Assert.All(report.Invariants, invariant => Assert.Equal(InvariantStatus.Pass, invariant.Status));
    }

}
