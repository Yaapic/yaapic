namespace Yaapic.Core.Runner;

/// <summary>The totals of a run.</summary>
/// <param name="Sent">The requests that got a response and were not skipped.</param>
/// <param name="Skipped">The skipped requests.</param>
/// <param name="Errors">The requests that ended in error.</param>
/// <param name="FailedRequests">The requests with a failed test.</param>
/// <param name="TestsPassed">The passed tests.</param>
/// <param name="TestsFailed">The failed tests.</param>
/// <param name="AverageResponseTime">The mean duration of the requests that got a response; zero when none.</param>
/// <param name="TotalDuration">The run duration.</param>
public sealed record RunSummary(
    int Sent,
    int Skipped,
    int Errors,
    int FailedRequests,
    int TestsPassed,
    int TestsFailed,
    TimeSpan AverageResponseTime,
    TimeSpan TotalDuration)
{
    /// <summary>Computes the totals of a run.</summary>
    /// <param name="result">The run result.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static RunSummary From(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RunRequestResult[] all = [.. result.Iterations.SelectMany(static i => i.Requests)];
        RunRequestResult[] answered = [.. all.Where(static r => r.StatusCode is not null)];
        return new RunSummary(
            Sent: answered.Count(static r => r.Outcome != RunRequestOutcome.Skipped),
            Skipped: all.Count(static r => r.Outcome == RunRequestOutcome.Skipped),
            Errors: all.Count(static r => r.Outcome == RunRequestOutcome.Error),
            FailedRequests: all.Count(static r => r.Outcome == RunRequestOutcome.Failed),
            TestsPassed: all.Sum(static r => r.Tests.Count(static t => t.Passed)),
            TestsFailed: all.Sum(static r => r.Tests.Count(static t => !t.Passed)),
            AverageResponseTime: answered.Length == 0 ? TimeSpan.Zero : TimeSpan.FromTicks((long)answered.Average(static r => r.Duration.Ticks)),
            TotalDuration: result.Duration);
    }
}
