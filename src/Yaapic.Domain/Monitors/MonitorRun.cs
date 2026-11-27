namespace Yaapic.Domain.Monitors;

/// <summary>The record of one monitor run.</summary>
/// <param name="MonitorId">The monitor identifier.</param>
/// <param name="At">When the run started.</param>
/// <param name="DurationMs">The run duration in milliseconds.</param>
/// <param name="Status">How the run ended.</param>
/// <param name="Requests">The requests that were not skipped.</param>
/// <param name="FailedRequests">The requests that failed a test or errored.</param>
/// <param name="TestsPassed">The passed tests.</param>
/// <param name="TestsFailed">The failed tests.</param>
/// <param name="Message">A one-line summary of the problem, or <see langword="null"/>.</param>
/// <param name="Failures">The first failing requests.</param>
public sealed record MonitorRun(
    Guid MonitorId,
    DateTimeOffset At,
    int DurationMs,
    MonitorRunStatus Status,
    int Requests,
    int FailedRequests,
    int TestsPassed,
    int TestsFailed,
    string? Message,
    IReadOnlyList<MonitorFailure> Failures);
