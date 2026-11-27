namespace Yaapic.TestDoubles;

/// <summary>
/// A <see cref="TimeProvider"/> whose time only moves when a test calls <see cref="Advance"/>, firing the timers that come due.
/// Timers are created and disposed from worker threads while a test advances the clock, so all state is guarded by a lock.
/// </summary>
/// <param name="start">The initial time.</param>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = start;

    /// <summary>Gets the number of timers that are armed, that is, waiting to fire at a future time.</summary>
    /// <remarks>A test that knows how many waiters it expects can wait on this count to know the code under test is idle again.</remarks>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(t => t.Due is not null);
            }
        }
    }

    /// <summary>Waits until exactly <paramref name="count"/> timers are armed, that is, until every worker of the code under test waits on the clock again.</summary>
    /// <param name="count">The expected number of armed timers.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the count is reached.</returns>
    /// <exception cref="TimeoutException">The count was not reached within 30 seconds.</exception>
    public Task WaitForPendingTimersAsync(int count, CancellationToken ct) => WaitUntilAsync(() => PendingTimers == count, $"{count} pending timers", ct);

    /// <summary>Polls a condition until it holds; the 30 second limit only bounds a broken test, a loaded machine just takes longer.</summary>
    /// <param name="condition">The condition.</param>
    /// <param name="what">What the test waits for, used in the timeout message.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the condition holds.</returns>
    /// <exception cref="TimeoutException">The condition did not hold within 30 seconds.</exception>
    public static async Task WaitUntilAsync(Func<bool> condition, string what, CancellationToken ct)
    {
        long deadline = Environment.TickCount64 + 30_000;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(1, ct);
        }
    }

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <summary>Moves the time forward and fires every timer that comes due on the way, in order.</summary>
    /// <param name="by">How far to move.</param>
    public void Advance(TimeSpan by)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _now + by;
        }

        while (true)
        {
            ManualTimer next;
            lock (_gate)
            {
                if (_timers.Where(t => t.Due is not null && t.Due <= target).MinBy(t => t.Due) is not { } due)
                {
                    _now = target;
                    return;
                }

                next = due;
                _now = next.Due!.Value;
                next.Rearm();
            }

            next.Invoke();
        }
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <inheritdoc/>
    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            timer.Change(dueTime, period);
            _timers.Add(timer);
        }

        return timer;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period;

        // Read and written under the owner's lock.
        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                _period = period;
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            }

            return true;
        }

        // Called with the owner's lock held, before the callback runs.
        public void Rearm() => Due = _period > TimeSpan.Zero ? Due + _period : null;

        public void Invoke() => callback(state);

        public void Dispose()
        {
            lock (owner._gate)
            {
                Due = null;
                owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
