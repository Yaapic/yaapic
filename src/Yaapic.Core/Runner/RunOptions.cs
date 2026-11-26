using Yaapic.Core.Scripting;

namespace Yaapic.Core.Runner;

/// <summary>The options of a run (ADR 0018).</summary>
/// <param name="Iterations">The number of iterations, 1..10000.</param>
/// <param name="DelayMs">The delay between two requests in milliseconds, 0..60000.</param>
/// <param name="StopOnFailure">Whether the run stops at the first failed or errored request.</param>
/// <param name="PersistVariables">Whether variable changes are kept after the run.</param>
/// <param name="Limits">The script limits, or <see langword="null"/> for the defaults.</param>
public sealed record RunOptions(
    int Iterations = 1,
    int DelayMs = 0,
    bool StopOnFailure = false,
    bool PersistVariables = true,
    ScriptLimits? Limits = null)
{
    /// <summary>Gets the default options: one iteration, no delay.</summary>
    public static RunOptions Default { get; } = new();

    /// <summary>Checks the option bounds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The iterations or the delay are out of range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Iterations, 10000);
        ArgumentOutOfRangeException.ThrowIfLessThan(DelayMs, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(DelayMs, 60000);
    }
}
