namespace Yaapic.Core.Runner;

/// <summary>The result of a whole run.</summary>
/// <param name="Name">The run name.</param>
/// <param name="StartedAt">When the run started.</param>
/// <param name="Duration">The total duration.</param>
/// <param name="Status">How the run ended.</param>
/// <param name="Error">The error text of a failed run, or <see langword="null"/>.</param>
/// <param name="Options">The options the run used.</param>
/// <param name="DataRows">The number of iteration data rows.</param>
/// <param name="Iterations">The iteration results.</param>
public sealed record RunResult(
    string Name,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    RunStatus Status,
    string? Error,
    RunOptions Options,
    int DataRows,
    IReadOnlyList<RunIterationResult> Iterations);
