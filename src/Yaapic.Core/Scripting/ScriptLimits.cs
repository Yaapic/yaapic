namespace Yaapic.Core.Scripting;

/// <summary>The limits an engine enforces on a script run (ADR 0009).</summary>
/// <param name="Timeout">The wall-clock limit of one block.</param>
/// <param name="MemoryLimitBytes">The memory limit of one run.</param>
/// <param name="MaxSendRequests">The number of <c>yaapic.sendRequest</c> calls allowed per run.</param>
/// <param name="MaxConsoleEntries">The number of console entries kept per send.</param>
/// <param name="MaxConsoleEntryLength">The characters kept per console entry (truncated with an ellipsis).</param>
public sealed record ScriptLimits(
    TimeSpan Timeout,
    long MemoryLimitBytes,
    int MaxSendRequests,
    int MaxConsoleEntries,
    int MaxConsoleEntryLength)
{
    /// <summary>Gets the default limits: 5 s, 64 MiB, 20 sends, 1000 console entries of 8192 characters.</summary>
    public static ScriptLimits Default { get; } = new(TimeSpan.FromSeconds(5), 64L * 1024 * 1024, 20, 1000, 8192);
}
