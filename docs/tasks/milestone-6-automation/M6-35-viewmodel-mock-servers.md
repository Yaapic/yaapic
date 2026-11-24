# M6-35: ViewModel: mock servers

Status: todo
Platform: any
Depends on: M6-21, M6-31, M6-34

## Goal

`MockServersViewModel` drives the Mock servers window: list, editor, start/stop, log, generation from a folder (ADR 0020, "UI").

## Design decisions

- Read first: ADR 0020, M6-27 to M6-34, `src/Yaapic.ViewModels/Environments/EnvironmentsViewModel.cs` (a master-detail editor with
  save and delete), `src/Yaapic.ViewModels/Editing/KeyValueEditorViewModel.cs` (reuse it for the route headers),
  `src/Yaapic.ViewModels/Tabs/TabsViewModel.cs` (how `ConfirmUnsavedAsync` choices are handled), M6-21 (`ContextProgress<T>` style marshalling: capture `SynchronizationContext.Current` in the constructor and
  post service events to it, inline when null).
- `src/Yaapic.ViewModels/Mocks/`:
  - `MockServerItemViewModel`: `Id`, `Name`, `Port`, `IsRunning`, `StatusText` (`Strings.MockRunning`/`MockStopped`), `BaseUrl`
    (`http://127.0.0.1:{port}`).
  - `MockRouteViewModel`: `Name`, `Enabled`, `Method` (from `MockMethods.All`), `Path`, `Status` (int), `Headers` (key/value editor),
    `Body`, `DelayMs`, `ToRoute()`; `IssueText` (the first issue of this route).
  - `MockLogRowViewModel`: `Time` ("HH:mm:ss"), `Method`, `Target`, `Status`, `Route` (name or `Strings.MockNoMatch`), `DurationText`,
    `Entry`.
  - `MockServersViewModel`, constructor `(MockServerService service, IDialogService dialogs)`:
    `Servers`, `SelectedServer`; editor fields of the selected server `Name`, `Port`, `Cors`, `Routes`, `SelectedRoute`, `IsDirty`;
    `NewServerCommand` (`MockServerDefinition.Create(Strings.MockNewName)` saved at once), `SaveCommand` (enabled when dirty; issues
    shown as `ValidationErrors` text lines, `Strings.MockIssue*`), `RevertCommand`, `DeleteServerCommand` (`ConfirmDeleteAsync`),
    `StartCommand`/`StopCommand` (errors -> `Error`), `CopyUrlCommand` (`SetClipboardTextAsync(BaseUrl)`), `AddRouteCommand`,
    `DuplicateRouteCommand`, `DeleteRouteCommand`, `MoveRouteUpCommand`/`MoveRouteDownCommand`; `Log` (rows of the selected server,
    newest first, refreshed on `RequestLogged` for that server), `SelectedLog` with `SelectedLogHeaders` and `SelectedLogBody`,
    `ClearLogCommand`; `Task GenerateFromFolderAsync(CollectionNode folder)` (generate, save, select; `GenerationMessage` =
    `Strings.MockGenerated(routes, skipped)`); `Task LoadAsync()`; `RunningCount` and `RunningText` (`Strings.MockStatusBar(n)`, empty when 0)
    for the status bar.
    Changing the selection with unsaved edits asks `ConfirmUnsavedAsync(name)` and handles its choices like `TabsViewModel`
    (save, discard, or cancel = keep the current selection).
- Strings (EN and FR): `mock.title` = "Mock servers"; `mock.new` = "New mock server"; `mock.newName` = "Mock server"; `mock.port` = "Port";
  `mock.cors` = "Allow cross-origin requests (CORS)"; `mock.start` = "Start"; `mock.stop` = "Stop"; `mock.running` = "Running";
  `mock.stopped` = "Stopped"; `mock.copyUrl` = "Copy URL"; `mock.routes` = "Routes"; `mock.addRoute` = "Add route"; `mock.duplicateRoute` =
  "Duplicate"; `mock.deleteRoute` = "Delete"; `mock.method` = "Method"; `mock.path` = "Path"; `mock.status` = "Status"; `mock.headers` =
  "Headers"; `mock.body` = "Body"; `mock.delay` = "Delay (ms)"; `mock.log` = "Requests"; `mock.clearLog` = "Clear"; `mock.noMatch` = "no match";
  `mock.generated` = "Created {0} routes ({1} duplicates skipped)."; `mock.statusBar` = "Mocks: {0} running"; `mock.pathHint` =
  "Use :name or {name} for a path parameter and * for the rest of the path."; `mock.templateHint` = "Templates: {{params.id}},
  {{query.name}}, {{headers.name}}, {{body}}, {{$guid}}"; one `mock.issue<Name>` string per `MockIssue` value.

## Out of scope

Shell entries (M6-41), windows (M6-47, M6-54).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests/Mocks/` with `InMemoryMockServerStore`, `FakeMockHttpHost`, `FakeDialogService`: load and select;
  new server; edit then save (and the store has it); validation errors shown and nothing saved; revert; unsaved-change prompt on selection
  change; delete with confirmation; start/stop and `RunningText`; port in use sets `Error`; route add/duplicate/delete/move; a request through
  the fake host appears in `Log` with its route name; log detail; clear log; copy URL; generation from a folder.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the mock servers view model`
