# M6-40: ViewModel: monitors

Status: done
Platform: any
Depends on: M6-21, M6-39

## Goal

`MonitorsViewModel` drives the Monitors window and the failing-monitors status item (ADR 0021, "Notifications", "UI").

## Design decisions

- Read first: ADR 0021, M6-36 and M6-39, M6-35 (master-detail editor, save/revert/unsaved prompt, event marshalling to the captured
  `SynchronizationContext`), `src/Yaapic.ViewModels/Console/ConsoleViewModel.cs` (`AppendSystem`).
- `src/Yaapic.ViewModels/Monitors/`:
  - `MonitorItemViewModel`: `Id`, `Name`, `FolderName` (or `Strings.MonitorFolderMissing`), `EnvironmentName` (or `Strings.MonitorNoEnvironment`),
    `IntervalText` (`Strings.MonitorEvery(minutes)` with "5 min", "1 h", "24 h" spellings), `Enabled` (toggle saves at once), `LastStatus`
    (`MonitorRunStatus?`), `LastStatusText`, `LastRunText` (local time "yyyy-MM-dd HH:mm" or `Strings.MonitorNever`), `NextRunText`,
    `IsFailing` (enabled and last status is not `Passed` and not null), `IsRunning`.
  - `MonitorRunRowViewModel`: `TimeText`, `Status`, `StatusText`, `DurationText`, `RequestsText` ("{failed}/{requests} failed"), `Message`,
    `Failures` (rows "request - outcome - status - detail").
  - `MonitorsViewModel`, constructor `(MonitorScheduler scheduler, IMonitorStore monitors, IMonitorRunStore runs, ICollectionStore collections,
    IEnvironmentStore environments, ConsoleViewModel console, IDialogService dialogs, TimeProvider time)`:
    `Task LoadAsync()`; `Monitors`; `SelectedMonitor`; editor `Name`, `FolderChoices` (every folder of the tree with its path, top-level
    folders first), `SelectedFolder`, `EnvironmentChoices` (none first, then the environments), `SelectedEnvironment`, `IntervalChoices`
    (`AllowedIntervals` with texts), `SelectedInterval`, `IsDirty`, `SaveCommand`, `RevertCommand`, unsaved prompt on selection change;
    `NewMonitorCommand` (for the first top-level folder; disabled when there is none), `Task NewForFolderAsync(Guid folderId)` (from the
    folder context menu: creates, saves, selects), `DeleteCommand` (`ConfirmDeleteAsync`, deletes the history too), `RunNowCommand`;
    `History` (last 50 runs of the selected monitor), `SelectedRun`.
  - On `scheduler.RunCompleted` (marshalled): refresh the item and the history when selected; when the status is not `Passed`,
    `console.AppendSystem(ConsoleLevel.Warn, Strings.MonitorFailed(name, message))`; update `FailingCount` and `FailingText`
    (`Strings.MonitorStatusBar(n)`, empty when 0). `StateChanged` updates `IsRunning`.
- Strings (EN and FR): `monitor.title` = "Monitors"; `monitor.new` = "New monitor"; `monitor.newName` = "Monitor"; `monitor.folder` = "Folder";
  `monitor.environment` = "Environment"; `monitor.noEnvironment` = "No environment"; `monitor.interval` = "Run every"; `monitor.every` =
  "Every {0}"; `monitor.enabled` = "Enabled"; `monitor.runNow` = "Run now"; `monitor.never` = "Never"; `monitor.lastRun` = "Last run";
  `monitor.nextRun` = "Next run"; `monitor.history` = "History"; `monitor.statusPassed` = "Passed"; `monitor.statusFailed` = "Failed";
  `monitor.statusError` = "Error"; `monitor.failed` = "Monitor '{0}' failed: {1}"; `monitor.statusBar` = "Monitors: {0} failing";
  `monitor.running` = "Running..."; `monitor.localNote` = "Monitors run only while Yaapic is open on this computer.".

## Out of scope

Shell entries (M6-41), composition (M6-42), windows.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests/Monitors/` with in-memory stores, a real `MonitorScheduler` over fake transport and engine, and a
  `ManualTimeProvider`: load lists monitors with folder and environment names; create, edit, save, revert, unsaved prompt; enabled toggle saves;
  delete removes history; run now records a run that appears in history; a failing run writes the console warning and updates
  `FailingCount`/`FailingText`; a passing run afterwards clears it; interval texts; next/last run texts.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the monitors view model`
