using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>Where a script runs: the iteration of a Runner run, its data row, or a single send.</summary>
/// <param name="Iteration">The zero-based iteration index.</param>
/// <param name="IterationCount">The number of iterations of the run.</param>
/// <param name="Data">The data row of the iteration; empty outside a data-driven run.</param>
/// <param name="InRunner">Whether the send belongs to a Runner run.</param>
public sealed record ScriptRunContext(int Iteration, int IterationCount, IReadOnlyList<KeyValueEntry> Data, bool InRunner)
{
    /// <summary>Gets the context of a send made outside the Runner.</summary>
    public static ScriptRunContext Single { get; } = new(0, 1, [], false);
}
