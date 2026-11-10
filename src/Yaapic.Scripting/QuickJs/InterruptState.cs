using System.Diagnostics;

namespace Yaapic.Scripting.QuickJs;

/// <summary>Deadline and cancellation flag read by the native interrupt handler.</summary>
internal sealed class InterruptState
{
    private long deadline;
    private volatile bool cancelled;
    private volatile bool fired;

    /// <summary>Whether the handler aborted the last run.</summary>
    internal bool Fired => fired;

    /// <summary>Aborts runs that are still going after <paramref name="timeout"/>.</summary>
    internal void SetTimeout(TimeSpan timeout) =>
        Volatile.Write(ref deadline, Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency));

    /// <summary>Aborts the running script as soon as the engine polls.</summary>
    internal void Cancel() => cancelled = true;

    /// <summary>Clears the deadline, the cancellation flag and <see cref="Fired"/>.</summary>
    internal void Reset()
    {
        Volatile.Write(ref deadline, 0);
        cancelled = false;
        fired = false;
    }

    /// <summary>Called by the engine every few thousand operations.</summary>
    internal bool ShouldAbort()
    {
        var limit = Volatile.Read(ref deadline);
        if (cancelled || (limit != 0 && Stopwatch.GetTimestamp() > limit))
        {
            fired = true;
            return true;
        }

        return false;
    }
}
