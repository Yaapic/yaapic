using Yaapic.Core.Scripting;

namespace Yaapic.Core.Runner;

/// <summary>A progress notification of a running run.</summary>
/// <param name="Iteration">The zero-based current iteration.</param>
/// <param name="IterationCount">The number of iterations.</param>
/// <param name="Completed">The requests completed so far.</param>
/// <param name="Total">Iterations times items; an estimate when <c>setNextRequest</c> is used.</param>
/// <param name="Last">The last request result, or <see langword="null"/> before the first.</param>
/// <param name="LastReport">The send report of the last request, for the console panel.</param>
public sealed record RunProgress(
    int Iteration,
    int IterationCount,
    int Completed,
    int Total,
    RunRequestResult? Last,
    ScriptedSendReport? LastReport);
