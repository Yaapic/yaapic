namespace Yaapic.TestDoubles;

/// <summary>A <see cref="TimeProvider"/> whose time only moves when a test calls <see cref="Advance"/>, firing the timers that come due.</summary>
/// <param name="start">The initial time.</param>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = start;

    /// <summary>Moves the time forward and fires every timer that comes due on the way, in order.</summary>
    /// <param name="by">How far to move.</param>
    public void Advance(TimeSpan by)
    {
        var target = _now + by;
        while (_timers.Where(t => t.Due is not null && t.Due <= target).MinBy(t => t.Due) is { } next)
        {
            _now = next.Due!.Value;
            next.Fire();
        }

        _now = target;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period;

        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            return true;
        }

        public void Fire()
        {
            Due = _period > TimeSpan.Zero ? Due + _period : null;
            callback(state);
        }

        public void Dispose()
        {
            Due = null;
            owner._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
