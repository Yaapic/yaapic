# M6-26: ViewModel: load test

Status: done
Platform: any
Depends on: M6-20, M6-21, M6-25

## Goal

`LoadTestViewModel` drives the Load test window: configuration, confirmation, live metrics and charts, results and export (ADR 0019, "UI").

## Design decisions

- Read first: ADR 0019, M6-20 (`RunnerSetupViewModel`: reuse it for the request list and the data file, ignoring its runner options),
  M6-21 (`ContextProgress<T>`, export pattern), M6-22 to M6-25, `src/Yaapic.Core/Responses/ResponseText.cs`.
- `src/Yaapic.ViewModels/LoadTesting/LoadTestViewModel.cs`, constructor `(RunnerSetupViewModel requests, LoadTestRunner runner,
  IVariableScopeProvider scopes, IDialogService dialogs)`:
  - `Requests` (the setup view model: list, selection, order, data file); `void Open(CollectionNode folder)` (ignored while running).
  - Configuration: `VirtualUsers` (default 10), `DurationSeconds` (default 60), `Profile` (`LoadProfile`, default `Fixed`),
    `RampUpSeconds` (default 10, visible when `RampUp`: `IsRampUp`), `ThinkTimeMs` (default 0); `ValidationError` with
    `Strings.LoadUsersRange` (1..`LoadTestPlan.MaxVirtualUsers`), `Strings.LoadDurationRange`, `Strings.LoadRampRange`,
    `Strings.LoadThinkRange`, or the setup's "select at least one request".
  - `StartCommand` (async; enabled when valid and not running): builds the plan (selected items, data rows of the setup); the
    first start of this view model instance asks for confirmation through a new `IDialogService` member
    `Task<bool> ConfirmAsync(string title, string message)` (default implementation returns `Task.FromResult(true)`; the platforms
    implement it in M6-43 and M6-50; `FakeDialogService` gets a settable `ConfirmAnswer`, default true, and records the messages).
    The title is `Strings.LoadConfirmTitle`, the message is `Strings.LoadConfirm(hosts)` with `VirtualUserSchedule.DescribeHosts(plan, url => VariableResolver.Resolve(url,
    scopes.GetScopes(firstItemId).CreateSource()).Text)`. Declined: nothing starts. Then `IsRunning`, run with a
    `ContextProgress<LoadSnapshot>`; `StopCommand` cancels.
  - Live: `ElapsedText` ("m:ss / m:ss"), `ActiveUsers`, tiles `TotalRequests`, `Throughput` ("12.3/s"), `AverageText`, `P95Text`,
    `ErrorRateText` ("1.2 %"); four charts as `IReadOnlyList<ChartPoint>` (`ChartPoint(double X, double Y)`, both 0..1, X = second /
    max(1, duration seconds), Y = value / max(value over the series, 1)): `ThroughputPoints`, `ResponseTimePoints`, `ErrorPoints`,
    `UserPoints`, plus `ThroughputMax`, `ResponseTimeMax` texts for the axis labels. Recomputed on each snapshot.
  - Results: `RequestRows` (name, method, count, errors, average, p90, p95, p99, max texts) from the final result, `ErrorMessages`,
    `Note` (truncation or "scripts were skipped"), `Result`.
  - `ExportCommand` (JSON report through `SaveFileAsync`), `ExportMessage`.
- Strings (EN and FR): `load.title` = "Load test"; `load.users` = "Virtual users"; `load.duration` = "Duration (s)"; `load.profile` = "Profile";
  `load.profileFixed` = "Fixed"; `load.profileRampUp` = "Ramp up"; `load.rampUp` = "Ramp-up (s)"; `load.thinkTime` = "Think time (ms)";
  `load.start` = "Start"; `load.stop` = "Stop"; `load.confirmTitle` = "Start a load test?"; `load.confirm` = "Start a load test against {0}?
  Only test servers you are allowed to load."; `load.usersRange` = "Virtual users must be between 1 and {0}."; `load.durationRange` =
  "The duration must be between 10 and 3600 seconds."; `load.rampRange` = "The ramp-up must be between 1 second and the duration.";
  `load.thinkRange` = "The think time must be between 0 and 10000 ms."; `load.requests` = "Requests"; `load.throughput` = "Throughput";
  `load.average` = "Average"; `load.p95` = "95th percentile"; `load.errorRate` = "Error rate"; `load.activeUsers` = "Active users";
  `load.chartThroughput` = "Requests per second"; `load.chartResponseTime` = "Average response time"; `load.chartErrors` = "Errors per second";
  `load.chartUsers` = "Virtual users"; `load.errors` = "Errors"; `load.export` = "Export JSON"; `load.truncated` = "Percentiles computed on
  the first {0} samples."; `load.scriptsSkipped` = "Scripts were not run during this load test.".

## Out of scope

Shell entries (M6-41), windows (M6-46, M6-53).

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests/LoadTesting/` with a real `LoadTestRunner` over a fake transport and short durations, or a
  fake runner wrapper if the real one makes tests slow (keep each test under 3 s): validation messages; confirmation asked once with the
  hosts, declined start does nothing; snapshots update tiles and normalized chart points (values between 0 and 1, X increasing);
  stop cancels; final rows and errors; export bytes and name; `IDialogService.ConfirmAsync` default returns true.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the load-test view model`
