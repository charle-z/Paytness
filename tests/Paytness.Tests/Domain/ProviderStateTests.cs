using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using Paytness.Provider;

namespace Paytness.Tests.Domain;

public sealed class ProviderStateTests
{
    [Fact]
    public void SameKeyAndPayloadReusesSameAttempt()
    {
        var state = new ProviderState();
        using JsonDocument body = JsonDocument.Parse("{\"merchantReference\":\"order-1\",\"amountMinor\":100}");
        ProviderProcessResult first = state.Process(body.RootElement, "order-1", "stable-key");
        ProviderProcessResult second = state.Process(body.RootElement, "order-1", "stable-key");
        ProviderMetrics metrics = state.GetMetrics();
        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Attempt.Id, second.Attempt.Id);
        Assert.Equal(2, metrics.RequestCount);
        Assert.Equal(1, metrics.AttemptCount);
        Assert.Equal(1, metrics.EconomicEffectCount);
    }

    [Fact]
    public void SameKeyAndDifferentPayloadIsConflictWithoutSecondEffect()
    {
        var state = new ProviderState();
        using JsonDocument first = JsonDocument.Parse("{\"merchantReference\":\"order-1\",\"amountMinor\":100}");
        using JsonDocument second = JsonDocument.Parse("{\"merchantReference\":\"order-1\",\"amountMinor\":200}");
        state.Process(first.RootElement, "order-1", "stable-key");
        ProviderProcessResult result = state.Process(second.RootElement, "order-1", "stable-key");
        Assert.True(result.IdempotencyConflict);
        Assert.Equal(1, state.GetMetrics().EconomicEffectCount);
    }

    [Fact]
    public void PropertySameKeyNeverCreatesMoreThanOneAttemptForSamePayload()
    {
        Property property = Prop.ForAll<int>(amount =>
        {
            var state = new ProviderState();
            using JsonDocument body = JsonDocument.Parse($"{{\"merchantReference\":\"order-p\",\"amountMinor\":{Math.Abs((long)amount)}}}");
            for (int index = 0; index < 10; index++) state.Process(body.RootElement, "order-p", "stable-key");
            ProviderMetrics metrics = state.GetMetrics();
            return metrics.RequestCount == 10 && metrics.AttemptCount == 1 && metrics.EconomicEffectCount == 1;
        });

        Check.QuickThrowOnFailure(property);
    }

    [Fact]
    public void HundredConcurrentSameKeyProcessesCreateOneEffect()
    {
        var state = new ProviderState();
        Parallel.For(0, 100, _ =>
        {
            using JsonDocument body = JsonDocument.Parse("{\"merchantReference\":\"order-c\",\"amountMinor\":100}");
            state.Process(body.RootElement, "order-c", "stable-key");
        });

        ProviderMetrics metrics = state.GetMetrics();
        Assert.Equal(100, metrics.RequestCount);
        Assert.Equal(1, metrics.AttemptCount);
        Assert.Equal(1, metrics.EconomicEffectCount);
        Assert.Equal(1, metrics.DistinctIdempotencyKeyCount);
    }

    [Fact]
    public void HundredDistinctKeysExposeHundredEffects()
    {
        var state = new ProviderState();
        Parallel.For(0, 100, index =>
        {
            using JsonDocument body = JsonDocument.Parse("{\"merchantReference\":\"order-c\",\"amountMinor\":100}");
            state.Process(body.RootElement, "order-c", $"key-{index:D3}");
        });

        ProviderMetrics metrics = state.GetMetrics();
        Assert.Equal(100, metrics.RequestCount);
        Assert.Equal(100, metrics.AttemptCount);
        Assert.Equal(100, metrics.EconomicEffectCount);
        Assert.Equal(100, metrics.DistinctIdempotencyKeyCount);
    }
}
