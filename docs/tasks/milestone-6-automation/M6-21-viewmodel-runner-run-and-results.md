# M6-21: ViewModel: runner run and results

Status: done
Platform: any
Depends on: M6-17, M6-18, M6-19, M6-20

## Goal

`RunnerViewModel` starts and stops a run, shows its progress and results, and exports the reports (ADR 0018, "UI").

## Design decisions

- Read first: ADR 0018, M6-14 to M6-20, `src/Yaapic.ViewModels/Updates/UpdateViewModel.cs` (the `InlineProgress` class posting
  reports to the starting `SynchronizationContext`), `src/Yaapic.ViewModels/Console/ConsoleViewModel.cs` (`AppendReport`),
  `src/Yaapic.ViewModels/Responses/TestResultsViewModel.cs`, `src/Yaapic.Core/Responses/ResponseText.cs` (duration and size texts).
- Move `UpdateViewModel.InlineProgress` to a shared generic `internal sealed class ContextProgress<T>(Action<T> report) : IProgress<T>`
  in `src/Yaapic.ViewModels/ContextProgress.cs` and use it from `UpdateViewModel` (no behaviour change).
- `src/Yaapic.ViewModels/Runner/VariableScopeRunSource.cs`: `public sealed class VariableScopeRunSource(IVariableScopeProvider provider)
  : IRunScopeSource` (`GetScopes`, `GetWritableScopes` forward; `PersistAsync` calls `ApplyChangesAsync`).
- `src/Yaapic.ViewModels/Runner/RunnerViewModel.cs`, constructor `(RunnerSetupViewModel setup, CollectionRunner runner,
  ConsoleViewModel console, IDialogService dialogs)`:
  - `Setup`; `void Open(CollectionNode folder)` loads the setup unless a run is going (then it only keeps the current state).
  - `RunCommand` (async; enabled when `Setup.CanRun` and not `IsRunning`): clears the results, `IsRunning = true`, runs
    `runner.RunAsync(Setup.BuildPlan(), progress, cts.Token)` with a `ContextProgress<RunProgress>` that appends `LastReport` to the
    console (`console.AppendReport`, plus its warnings with `AppendSystem(Warn, ...)` as the request editor does) and adds the row;
    at the end sets `Result`, the summary and the status text. `StopCommand` cancels. `IsRunning`, `ProgressText` ("{completed}/{total}"),
    `Progress` (0..1). Raises `RunStarted`, `RunCompleted` (`EventHandler<RunResult>`).
  - Results: `Rows` (`ObservableCollection<RunnerResultRowViewModel>`: `Iteration` (1-based), `Name`, `Path`, `Method`, `Outcome`,
    `OutcomeText`, `StatusText` (code or "-"), `DurationText`, `SizeText`, `TestsText` ("passed/total" via `TestSummary.Format`),
    `Result` (the `RunRequestResult`)), `Filter` (`RunnerFilter` enum `All`, `Passed`, `Failed` (Failed or Error), `Skipped`) and
    `VisibleRows`; `SelectedRow` with `SelectedTests` (`TestResult` list), `SelectedConsole` (lines as text "[level] message"),
    `SelectedError`.
  - Summary: `SummarySent`, `SummaryErrors`, `SummaryFailed`, `SummarySkipped`, `SummaryTests` ("passed/total"), `SummaryAverage`
    (duration text), `SummaryDuration`, `StatusText` (`Strings.RunnerStatus{Completed|Stopped|StoppedOnFailure|Canceled|Failed}`, the
    `Failed` one with the error).
  - Export: `ExportJsonCommand` / `ExportJUnitCommand` (enabled when `Result` is set): `dialogs.SaveFileAsync(SuggestedFileName,
    bytes)`; `ExportMessage` = `Strings.RunnerExported(name)` or nothing when canceled.
- Strings (EN and FR): `runner.run` = "Run"; `runner.stop` = "Stop"; `runner.results` = "Results"; `runner.filterAll` = "All";
  `runner.filterPassed` = "Passed"; `runner.filterFailed` = "Failed"; `runner.filterSkipped` = "Skipped"; `runner.outcomePassed` = "Passed";
  `runner.outcomeFailed` = "Failed"; `runner.outcomeError` = "Error"; `runner.outcomeSkipped` = "Skipped"; `runner.iteration` = "Iteration {0}";
  `runner.summary` = "{0} sent, {1} errors, {2} failed, {3} skipped"; `runner.statusCompleted` = "Run completed";
  `runner.statusStopped` = "Run stopped by a script"; `runner.statusStoppedOnFailure` = "Run stopped on the first failure";
  `runner.statusCanceled` = "Run canceled"; `runner.statusFailed` = "Run failed: {0}"; `runner.exportJson` = "Export JSON";
  `runner.exportJUnit` = "Export JUnit XML"; `runner.exported` = "Saved {0}"; `runner.running` = "Running {0}/{1}".

## Out of scope

Shell entries and status bar (M6-41), composition (M6-42), the windows.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests/Runner/` with a real `CollectionRunner` over `FakeHttpTransport` and `FakeScriptEngine`, a
  `FakeVariableScopeProvider` behind `VariableScopeRunSource`, `FakeDialogService`: a run fills rows, summary and status; the console
  received the reports; filters; selection details; stop cancels and shows the canceled status; `Run` disabled while running and
  when the setup cannot run; `Open` during a run does not reset; export JSON and JUnit write the bytes with the suggested names;
  canceled save leaves no message; `UpdateViewModel` tests still pass with `ContextProgress`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the runner view model`
