using System.Threading.Channels;

namespace Paytness.Execution;

public sealed record ScheduledAction(
    string Id,
    int Ordinal,
    TimeSpan Delay,
    Func<CancellationToken, ValueTask> ExecuteAsync);

public readonly record struct ScheduleKey(long DueUtcTicks, int Ordinal);

public static class ScheduledActionScheduler
{
    public const int MaximumActions = 2_000;

    public static async Task<IReadOnlyList<string>> ExecuteAsync(
        IReadOnlyList<ScheduledAction> actions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (actions.Count > MaximumActions)
            throw new InvalidOperationException($"Scheduled action count exceeds the {MaximumActions} action limit.");
        if (actions.Any(static action => action.Ordinal < 1 || action.Delay < TimeSpan.Zero))
            throw new InvalidOperationException("Scheduled actions require positive ordinals and non-negative delays.");

        var channel = Channel.CreateBounded<ScheduledAction>(new BoundedChannelOptions(MaximumActions)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        DateTimeOffset start = timeProvider.GetUtcNow();

        foreach (ScheduledAction action in actions)
            await channel.Writer.WriteAsync(action, cancellationToken);
        channel.Writer.Complete();

        var queue = new PriorityQueue<ScheduledAction, ScheduleKey>(ScheduleKeyComparer.Instance);
        await foreach (ScheduledAction action in channel.Reader.ReadAllAsync(cancellationToken))
        {
            DateTimeOffset due = start + action.Delay;
            queue.Enqueue(action, new ScheduleKey(due.UtcTicks, action.Ordinal));
        }

        var completed = new List<string>(actions.Count);
        while (queue.TryDequeue(out ScheduledAction? action, out ScheduleKey priority))
        {
            TimeSpan remaining = new DateTimeOffset(priority.DueUtcTicks, TimeSpan.Zero) - timeProvider.GetUtcNow();
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, timeProvider, cancellationToken);

            await action.ExecuteAsync(cancellationToken);
            completed.Add(action.Id);
        }

        return completed;
    }

    private sealed class ScheduleKeyComparer : IComparer<ScheduleKey>
    {
        public static ScheduleKeyComparer Instance { get; } = new();

        public int Compare(ScheduleKey left, ScheduleKey right)
        {
            int dueComparison = left.DueUtcTicks.CompareTo(right.DueUtcTicks);
            return dueComparison != 0 ? dueComparison : left.Ordinal.CompareTo(right.Ordinal);
        }
    }
}
