# M6-24: Core: load-test runner

Status: todo
Platform: any
Depends on: M6-16, M6-22, M6-23

## Goal

Run the virtual users of a `LoadTestPlan` with the runner's step executor and feed `LoadMetrics` (ADR 0019).

## Design decisions

- Read first: ADR 0019 ("A virtual user", "End", "Sample"), `docs/spikes/m6-load.md` (decision (2): scripts on or off), M6-16
  (`RunStepExecutor`, `RunStepResult`), M6-22, M6-23.
- `src/Yaapic.Core/LoadTesting/LoadTestResult.cs`: `LoadTestResult(LoadTestPlan Plan, DateTimeOffset StartedAt, TimeSpan Duration,
  bool Canceled, LoadStats Overall, IReadOnlyList<LoadStats> PerRequest, IReadOnlyList<LoadBucket> Buckets,
  IReadOnlyList<string> ErrorMessages, bool SamplesTruncated)`.
- `src/Yaapic.Core/LoadTesting/LoadTestRunner.cs`: constructor `(RunStepExecutor step, IRunScopeSource scopes, TimeProvider time)`;
  `Task<LoadTestResult> RunAsync(LoadTestPlan plan, IProgress<LoadSnapshot>? progress, CancellationToken ct)`:
  - `plan.Validate()`; `start = time.GetTimestamp()`; one `LoadMetrics`; a linked token source `stop` for the end of the duration;
  - one task per virtual user (`Task.Run`), started at `VirtualUserSchedule.StartOffset` (`Task.Delay(offset, time, ct)`); each user
    owns a `RunVariableState` and loops: take the next data row (`Interlocked.Increment` on a shared counter, modulo the row count;
    empty row without data), then for each item in order: stop if the duration is over or `ct` is canceled; execute the step with
    `new ScriptRunContext(userIteration, 0, row.Values, InRunner: true)` (`IterationCount` 0 = unknown in a load test) and
    `persist: false`; ignore `NextRequestSet`; a `Skipped` result is not a sample; otherwise add a sample
    (`IsError` = outcome `Error` or `Failed` or status >= 400; status 0 when none) and, for an error with a message, `AddError`;
    then wait `ThinkTimeMs` (not after the end);
  - in-flight requests at the end of the duration use the caller's `ct` only (they finish); cancellation of `ct` cancels them and the
    result has `Canceled = true`;
  - a 1-second `PeriodicTimer(TimeSpan.FromSeconds(1), time)` loop reports `metrics.Snapshot(elapsed)` with the number of users
    currently running, and calls `SetActiveUsers`; a final snapshot is reported at the end;
  - never throws on cancellation; an unexpected exception in a user is recorded with `AddError(ex.Message)` and that user stops.
- If the spike decided that scripts do not run in load tests: the steps are executed with an empty script chain (wrap
  `IScriptChainProvider` with one returning `ScriptChain.Empty` and strip `Request.Scripts`) and `LoadTestResult` gains
  `bool ScriptsSkipped = true`. Otherwise scripts run normally.

## Out of scope

JSON report (M6-25), view model (M6-26).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests/LoadTesting/` with a fake transport (fixed latency through `Task.Delay` on the `ManualTimeProvider`
  or real time with short durations; keep each test under 3 s of wall time): Fixed 3 users x 10 s on the manual clock produce samples
  from 3 users; RampUp starts users at the scheduled offsets; think time; errors counted for a 500 status and for a failed test; a
  skipped request is not a sample; data rows taken round-robin across users; cancellation returns `Canceled` with metrics; snapshots
  reported every second; variable changes never persisted (`PersistAsync` never called).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the load-test runner`
