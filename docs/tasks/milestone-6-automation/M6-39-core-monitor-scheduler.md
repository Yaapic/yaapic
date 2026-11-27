# M6-39: Core: monitor scheduler

Status: done
Platform: any
Depends on: M6-16, M6-36

## Goal

Run the due monitors one at a time while the app is open, and record each run (ADR 0021, "Run", "Schedule", "Notifications").

## Design decisions

- Read first: ADR 0021, M6-16 (`CollectionRunner`, `RunStepExecutor`, `IRunScopeSource`), M6-36, `src/Yaapic.ViewModels/Updates/UpdateViewModel.cs`
  (`PeriodicTimer` with a `TimeProvider`), `src/Yaapic.Core/Storage/ICollectionStore.cs`, `IEnvironmentStore.cs`.
- `src/Yaapic.Core/Monitors/MonitorScheduler.cs` (public sealed, `IAsyncDisposable`), constructor
  `(IMonitorStore monitors, IMonitorRunStore runs, ICollectionStore collections, IEnvironmentStore environments, IRunScopeSource scopes,
  RunStepExecutor step, TimeProvider time)`:
  - `void Start()` (idempotent): a background loop: tick at once, then every 30 s (`PeriodicTimer(TimeSpan.FromSeconds(30), time)`).
    `Task StopAsync()` cancels the loop and the current run and waits for it; `DisposeAsync` = `StopAsync`.
  - Tick: list monitors; for each enabled one, `latest = await runs.LatestAsync(id)`; due when `latest is null` or
    `now >= latest.At + interval`; enqueue due ones not already queued or running, ordered by due time (never-run first, then oldest due).
  - `void RunNow(Guid monitorId)`: enqueues it even when disabled (still never twice).
  - One worker runs the queue sequentially: reload the monitor (deleted: drop it); find the folder in `collections.LoadTreeAsync`
    (`CollectionNode.Flatten`) — missing: record `MonitorRuns.Error(id, now, Strings.MonitorFolderMissing)`; load the environment by id
    when set (`environments` list) — missing: `Strings.MonitorEnvironmentMissing`; build `RunPlan(monitor.Name, RunPlanBuilder.ListRequests(folder),
    new RunOptions(1, 0, false, PersistVariables: false), [])`; run a `CollectionRunner` with a scope source wrapper that returns
    `inner.GetScopes(id) with { Environment = monitorEnvironment }` (null when the monitor has none) and never persists
    (`PersistAsync` returns `[]` without calling the inner source; `GetWritableScopes` returns `[]`); record `MonitorRuns.From`;
    raise `RunCompleted` (`EventHandler<MonitorRun>`).
  - Errors from the stores are caught, recorded as `MonitorRuns.Error` when possible, and never stop the loop.
  - `Guid? Running` (current monitor), `IReadOnlyList<Guid> Queued`, event `StateChanged`.
  - `static DateTimeOffset? NextRun(MonitorDefinition monitor, MonitorRun? latest, DateTimeOffset now)` (public): null when disabled;
    `latest.At + interval`, or `now` when it never ran.
- Strings (EN and FR): `monitor.folderMissing` = "The folder of this monitor no longer exists.";
  `monitor.environmentMissing` = "The environment of this monitor no longer exists.".

## Out of scope

Notifications in the console and the status bar (view model M6-40), the window.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Monitors/` with in-memory stores, `FakeHttpTransport`, `FakeScriptEngine`, `ManualTimeProvider`:
  a never-run enabled monitor runs at start; it runs again only after its interval (advance the clock past ticks); a disabled one does
  not, unless `RunNow`; two due monitors run one after the other, never in parallel (gate the transport); the monitor's environment is
  used, not the active one; variable changes are not persisted; missing folder and missing environment record `Error` runs; `StopAsync`
  cancels the current run; `RunCompleted` raised with the recorded run; `NextRun`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): schedule monitor runs`
