# M6-42: Composition: wire the M6 services

Status: todo
Platform: any
Depends on: M6-28, M6-33, M6-37, M6-38, M6-41

## Goal

`AppComposition.CreateShell` builds the runner, load-test runner, mock servers and monitor scheduler and attaches their view models to
the shell, as M5-54 did for import and export.

## Design decisions

- Read first: M5-54, `src/Yaapic.Composition/AppComposition.cs` (the internal `CreateShell` overload, `scriptedSender`, `VariableScopeProvider`,
  `CollectionTreeScriptChainProvider`, `AttachImportExport`, the secret-aware stores, `FileImportInputReader`), `docs/architecture.md`.
- In the internal `CreateShell` (build once, share; only when `scripting` is set, i.e. the real app; otherwise skip `AttachAutomation`):
  - `var runSource = new VariableScopeRunSource(scopes); var chains = new CollectionTreeScriptChainProvider(collections);`
    `var step = new RunStepExecutor(scriptedSender, chains, runSource);`
  - Runner: `new RunnerViewModel(new RunnerSetupViewModel(importReader, dialogs), new CollectionRunner(step, time), console, dialogs)`.
  - Load test: `new LoadTestViewModel(new RunnerSetupViewModel(importReader, dialogs), new LoadTestRunner(step, runSource, time), scopes, dialogs)`.
  - Mocks: `var mockService = new MockServerService(new FileMockServerStore(<workspace root as the other file stores get it>),
    new TcpMockHttpHost(time), time)`, `new MockServersViewModel(mockService, dialogs)`; the composition starts `mocks.LoadAsync()`
    without awaiting it (`CreateShell` is synchronous) and writes a failure to the console as a system warning. No server starts.
  - The `FileImportInputReader` instance created for import (M5-54) is shared with the runner and load-test setups (`importReader`).
  - Monitors: `new MonitorScheduler(new FileMonitorStore(workspace), new FileMonitorRunStore(workspace), collectionStore (secret-aware),
    environmentStore (secret-aware), runSource, step, time)`, `MonitorsViewModel(...)`; `scheduler.Start()` after the shell is built and
    `monitors.LoadAsync()` fire-and-forget (errors to the console).
  - `shell.AttachAutomation(runner, load, mocks, monitors)`; `shell.OwnAsync(mockService)`; `shell.OwnAsync(scheduler)`.
  - Use one `TimeProvider` for every M6 service: the internal `CreateShell` overload gains an optional `TimeProvider? time = null`
    parameter (null = the instance the shell already uses, `TimeProvider.System` in the app) so that the end-to-end tests of M6-59 can pass a
    `ManualTimeProvider`.
- The mock host and the scheduler must not start any socket or run during the existing composition tests unless those tests ask for it:
  `scheduler.Start()` runs a first tick at once, which is harmless on an empty workspace (no monitors).
- No new package; `docs/dependencies.md` unchanged.

## Out of scope

Views, docs (M6-59).

## Acceptance criteria

- [ ] A test in `Yaapic.Infrastructure.Tests` (where M5-54 tested the composition) on a temporary workspace with a loopback server:
  the shell has the four tool view models; the Runner runs a two-request folder against the loopback server and the results show both;
  a mock server created through `Mocks` starts on a free port and answers a request made with `HttpClient`; a monitor created through
  `Monitors` and `RunNow` records a run in `.yaapic/monitor-runs.jsonl`; closing the shell stops the mock server (port free again) and the
  scheduler; the palette lists the four entries.
- [ ] AOT publish of `Yaapic.App.WinUI` (the host-RID app used by M5-54): 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(composition): wire the runner, load tests, mock servers and monitors`
