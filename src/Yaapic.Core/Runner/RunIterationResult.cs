namespace Yaapic.Core.Runner;

/// <summary>The results of one iteration.</summary>
/// <param name="Index">The zero-based iteration index.</param>
/// <param name="Requests">The request results, in execution order.</param>
public sealed record RunIterationResult(int Index, IReadOnlyList<RunRequestResult> Requests);
