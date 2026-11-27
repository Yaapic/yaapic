namespace Yaapic.Core.Runner;

/// <summary>What a run executes: the requests, the options and the iteration data.</summary>
/// <param name="Name">The run name, usually the folder name.</param>
/// <param name="Items">The requests, in run order.</param>
/// <param name="Options">The run options.</param>
/// <param name="Data">The iteration data rows; empty without data.</param>
public sealed record RunPlan(string Name, IReadOnlyList<RunItem> Items, RunOptions Options, IReadOnlyList<IterationDataRow> Data)
{
    /// <summary>Gets the data row of an iteration.</summary>
    /// <param name="iteration">The zero-based iteration index.</param>
    /// <returns>Row <paramref name="iteration"/>, the last row past the end, or <see cref="IterationDataRow.Empty"/> without data.</returns>
    public IterationDataRow RowFor(int iteration)
    {
        if (Data.Count == 0)
        {
            return IterationDataRow.Empty;
        }

        return Data[Math.Clamp(iteration, 0, Data.Count - 1)];
    }
}
