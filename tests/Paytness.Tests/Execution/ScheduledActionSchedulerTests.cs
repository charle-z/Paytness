using Microsoft.Extensions.Time.Testing;
using Paytness.Execution;

namespace Paytness.Tests.Execution;

public sealed class ScheduledActionSchedulerTests
{
    [Fact]
    public async Task DueTimeThenOrdinalDetermineStableExecutionOrder()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var observed = new List<string>();
        ScheduledAction[] actions =
        [
            new("processing", 1, TimeSpan.FromMilliseconds(100), _ => { observed.Add("processing"); return ValueTask.CompletedTask; }),
            new("succeeded", 2, TimeSpan.Zero, _ => { observed.Add("succeeded"); return ValueTask.CompletedTask; }),
            new("duplicate", 3, TimeSpan.Zero, _ => { observed.Add("duplicate"); return ValueTask.CompletedTask; }),
        ];

        Task<IReadOnlyList<string>> run = ScheduledActionScheduler.ExecuteAsync(actions, time, token);
        for (int index = 0; index < 10 && !run.IsCompleted; index++)
        {
            await Task.Yield();
            time.Advance(TimeSpan.FromMilliseconds(20));
        }

        IReadOnlyList<string> completed = await run.WaitAsync(token);
        Assert.Equal(["succeeded", "duplicate", "processing"], completed);
        Assert.Equal(completed, observed);
    }

    [Fact]
    public async Task SchedulerRejectsMoreThanHardMaximum()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        ScheduledAction[] actions = Enumerable.Range(1, ScheduledActionScheduler.MaximumActions + 1)
            .Select(index => new ScheduledAction($"a-{index}", index, TimeSpan.Zero, _ => ValueTask.CompletedTask))
            .ToArray();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ScheduledActionScheduler.ExecuteAsync(actions, TimeProvider.System, token));

        Assert.Contains("2000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationStopsPendingScheduledAction()
    {
        using var cancellation = new CancellationTokenSource();
        ScheduledAction[] actions =
        [
            new("pending", 1, TimeSpan.FromMinutes(5), _ => ValueTask.CompletedTask),
        ];

        Task<IReadOnlyList<string>> run = ScheduledActionScheduler.ExecuteAsync(actions, TimeProvider.System, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }
}
