# M6-41: ViewModel: palette commands, shell entries and status items for the M6 tools

Status: todo
Platform: any
Depends on: M6-21, M6-26, M6-35, M6-40

## Goal

The shell offers the Runner, Load test, Mock servers and Monitors: events the views open windows from, folder context-menu commands,
palette entries and the status-bar texts (ADRs 0018 to 0021, "UI").

## Design decisions

- Read first: M5-53 (the same card for import and export: events, `Attach*`, `Create*`, palette entries and tests),
  `src/Yaapic.ViewModels/Shell/ShellViewModel.cs` (`AttachImportExport`, `ExportCollectionCommand`, `Own`, the close sequence that disposes
  `_ownedResources`), `src/Yaapic.ViewModels/Palette/CommandsPaletteSource.cs`, `src/Yaapic.ViewModels/Collections/CollectionTreeViewModel.cs`
  (`FindCollection`, node view models).
- `ShellViewModel`:
  - `void AttachAutomation(RunnerViewModel runner, LoadTestViewModel load, MockServersViewModel mocks, MonitorsViewModel monitors)`
    (once; a second call throws `InvalidOperationException`); `bool CanAutomate`. One instance of each tool view model lives as long as the
    shell (a run, a load test or a mock server keeps going when its window closes): properties `Runner`, `LoadTest`, `Mocks`, `Monitors`
    (null until attached).
  - Events: `RunnerRequested`, `LoadTestRequested`, `MockServersRequested`, `MonitorsRequested` (`EventHandler`); the views open (or
    activate) the matching window.
  - Commands with a `CollectionNodeViewModel` parameter, enabled for folder nodes (any depth) when attached:
    `RunFolderCommand` (`Runner.Open(node folder)` then `RunnerRequested`), `LoadTestFolderCommand` (`LoadTest.Open`, `LoadTestRequested`),
    `MockFromFolderCommand` (`await Mocks.GenerateFromFolderAsync(folder)`, `MockServersRequested`), `MonitorFolderCommand`
    (`await Monitors.NewForFolderAsync(folder.Id)`, `MonitorsRequested`). The node's `CollectionNode` comes from the tree
    (`Collections` lookup by id; add a lookup method if none exists).
  - Plain methods `RequestRunner()`, `RequestLoadTest()`, `RequestMockServers()`, `RequestMonitors()` (raise only).
  - `void OwnAsync(IAsyncDisposable resource)`: the close sequence awaits `DisposeAsync` of these after the owned `IDisposable`s.
  - Status bar: `RunnerStatusText` (`Strings.RunnerStatusBar` "Runner" when idle, `Runner.ProgressText` prefixed "Runner " while running),
    `MocksStatusText` (= `Mocks.RunningText`, empty hides the item), `MonitorsStatusText` (= `Monitors.FailingText`), each raising
    `PropertyChanged` when the source changes.
- `CommandsPaletteSource` (only when attached): `run-collection` (`Strings.PaletteRunCollection`: opens the Runner on the active tab's
  top-level folder when the active request belongs to one, else just opens the Runner window), `load-test` (`Strings.PaletteLoadTest`, same
  rule), `mock-servers` (`Strings.PaletteMockServers`), `monitors` (`Strings.PaletteMonitors`).
- Strings (EN and FR): `palette.runCollection` = "Run collection..."; `palette.loadTest` = "Load test..."; `palette.mockServers` = "Mock servers";
  `palette.monitors` = "Monitors"; `collections.runFolder` = "Run folder..."; `collections.loadTest` = "Load test...";
  `collections.createMock` = "Create a mock server..."; `collections.monitorFolder` = "Monitor folder..."; `runner.statusBar` = "Runner".

## Out of scope

Composition (M6-42), windows and menus (WinUI and Avalonia cards).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests` (tool view models built over fakes as in M6-21, M6-26, M6-35, M6-40): attach twice throws; not
  attached: commands disabled, events not raised, palette entries absent; each folder command opens/creates and raises its event, and is
  disabled for a request node; palette entries listed and run (with and without an active collection request); status texts follow the
  tool view models; `OwnAsync` resources disposed on close after the `IDisposable` ones.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the automation entries to the shell and the palette`
