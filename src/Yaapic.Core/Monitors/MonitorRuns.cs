using Yaapic.Core.Runner;
using Yaapic.Domain.Monitors;

namespace Yaapic.Core.Monitors;

/// <summary>Builds <see cref="MonitorRun"/> records.</summary>
public static class MonitorRuns
{
    private const int MaxFailures = 20;

    /// <summary>Summarizes a run result.</summary>
    /// <param name="monitorId">The monitor identifier.</param>
    /// <param name="result">The run result.</param>
    /// <returns>The run record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static MonitorRun From(Guid monitorId, RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        RunRequestResult[] all = [.. result.Iterations.SelectMany(static i => i.Requests)];
        RunRequestResult[] failing = [.. all.Where(static r => r.Outcome is RunRequestOutcome.Failed or RunRequestOutcome.Error)];
        RunSummary summary = RunSummary.From(result);
        MonitorFailure[] failures = [.. failing.Take(MaxFailures).Select(ToFailure)];
        MonitorRunStatus status = result.Status == RunStatus.Failed ? MonitorRunStatus.Error
            : failing.Length > 0 ? MonitorRunStatus.Failed : MonitorRunStatus.Passed;
        string? message = result.Error ?? (failures.Length > 0 ? $"{failures[0].RequestName}: {failures[0].Detail}" : null);
        return new MonitorRun(
            monitorId, result.StartedAt, (int)result.Duration.TotalMilliseconds, status,
            all.Count(static r => r.Outcome != RunRequestOutcome.Skipped), failing.Length,
            summary.TestsPassed, summary.TestsFailed, message, failures);
    }

    /// <summary>Creates the record of a run that could not start.</summary>
    /// <param name="monitorId">The monitor identifier.</param>
    /// <param name="at">When the run was attempted.</param>
    /// <param name="message">What went wrong.</param>
    /// <returns>The run record.</returns>
    public static MonitorRun Error(Guid monitorId, DateTimeOffset at, string message) =>
        new(monitorId, at, 0, MonitorRunStatus.Error, 0, 0, 0, 0, message, []);

    private static MonitorFailure ToFailure(RunRequestResult r)
    {
        var test = r.Tests.FirstOrDefault(static t => !t.Passed);
        string detail = test is not null
            ? (test.Message is { Length: > 0 } m ? $"{test.Name}: {m}" : test.Name)
            : r.Error ?? string.Empty;
        return new MonitorFailure(r.Name, r.Outcome.ToString(), r.StatusCode, detail);
    }
}
