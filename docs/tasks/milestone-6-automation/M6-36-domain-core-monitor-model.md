# M6-36: Domain and Core: monitor model and store ports

Status: done
Platform: any
Depends on: M6-05, M6-14

## Goal

The monitor definition, the run record and the two store ports of ADR 0021, with in-memory doubles.

## Design decisions

- Read first: ADR 0021 ("Definition", "Run", "Status of a run", "History"), M6-14 (`RunResult`, `RunSummary`, `RunRequestOutcome`),
  `src/Yaapic.Core/Storage/IEnvironmentStore.cs`, `IHistoryStore.cs`.
- Domain, `src/Yaapic.Domain/Monitors/`:
  - `MonitorDefinition(Guid Id, string Name, Guid FolderId, Guid? EnvironmentId, int IntervalMinutes, bool Enabled)` with
    `static IReadOnlyList<int> AllowedIntervals` = 5, 10, 15, 30, 60, 180, 360, 720, 1440, `static MonitorDefinition Create(string name,
    Guid folderId)` (60 minutes, enabled, no environment) and `bool IsValid` (name not blank and interval allowed).
  - `MonitorRunStatus` enum: `Passed`, `Failed`, `Error`.
  - `MonitorFailure(string RequestName, string Outcome, int? StatusCode, string Detail)`.
  - `MonitorRun(Guid MonitorId, DateTimeOffset At, int DurationMs, MonitorRunStatus Status, int Requests, int FailedRequests,
    int TestsPassed, int TestsFailed, string? Message, IReadOnlyList<MonitorFailure> Failures)`.
- Core, `src/Yaapic.Core/Monitors/`:
  - `MonitorRuns.From(Guid monitorId, RunResult result)` (static): status `Error` when `result.Status` is `Failed`, else `Failed` when any
    request is `Failed` or `Error`, else `Passed`; `Requests` = results that are not `Skipped`; `FailedRequests` = `Failed` + `Error`; tests
    counts from `RunSummary`; `Failures`: the first 20 failing requests in order, `Detail` = the first failed test name (with its message
    when there is one, `"{name}: {message}"`) or the error; `Message` = `result.Error` or the first failure `"{request}: {detail}"`;
    `At = result.StartedAt`; duration in whole ms.
    `MonitorRuns.Error(Guid monitorId, DateTimeOffset at, string message)` for a run that could not start.
  - `IMonitorStore` (`Core.Storage`): `ListAsync`, `SaveAsync`, `DeleteAsync` with the contract of `IMockServerStore` (M6-28).
  - `IMonitorRunStore` (`Core.Storage`): `Task AppendAsync(MonitorRun run, CancellationToken ct)` (then keeps at most 200 runs of that
    monitor), `Task<IReadOnlyList<MonitorRun>> ListAsync(Guid monitorId, int max, CancellationToken ct)` (newest first),
    `Task<MonitorRun?> LatestAsync(Guid monitorId, CancellationToken ct)`, `Task DeleteAsync(Guid monitorId, CancellationToken ct)`;
    `public const int MaxRunsPerMonitor = 200`.
- `tests/Yaapic.TestDoubles/InMemoryMonitorStore.cs` and `InMemoryMonitorRunStore.cs`.

## Out of scope

File stores (M6-37, M6-38), the scheduler (M6-39).

## Acceptance criteria

- [x] Tests: `MonitorDefinition.Create`/`IsValid` (Domain tests); `MonitorRuns.From` for a passing run, a failed test, an error request, a
  skipped-only run, a `Failed` run status (setNextRequest loop), 25 failures capped at 20, detail formatting (Core tests); in-memory
  run store keeps 200 per monitor and lists newest first.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the monitor model and store ports`
