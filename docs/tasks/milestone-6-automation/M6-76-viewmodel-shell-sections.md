# M6-76: ViewModel: shell sections for the automation views

Status: done
Platform: any
Depends on: M6-75

## Goal

The shell knows what its main area shows (the requests or one of the four tools), offers rail commands for the tools, goes back to
the requests when a tab is shown, and asks before closing while something runs (ADR 0023). The views do not bind to it yet, so the app
behaves as before after this card (the tool windows still open).

## Design decisions

- Read first: ADR 0023, M6-41, `src/Yaapic.ViewModels/Shell/ShellViewModel.cs` (`Mode`, `ShowCollections`, `ShowHistory`,
  `RequestCloseAsync`), `ShellViewModel.Automation.cs`, `SidebarMode.cs`, `src/Yaapic.ViewModels/Tabs/TabsViewModel.cs`,
  `src/Yaapic.ViewModels/Palette/CommandsPaletteSource.cs`, `tests/Yaapic.ViewModels.Tests/AutomationShellTests.cs`,
  `ShellViewModelTests.cs`, `TabsViewModelTests.cs`.
- `src/Yaapic.ViewModels/Shell/ShellSection.cs`: `public enum ShellSection { Requests, Runner, LoadTest, MockServers, Monitors }`, each
  member documented ("What the main area shows.").
- `ShellViewModel` (put the new members in `ShellViewModel.Automation.cs`, except where an existing member changes):
  - `[ObservableProperty] private ShellSection _section = ShellSection.Requests;` with `[NotifyPropertyChangedFor]` of every bool below.
  - Read-only bools for the views (both front-ends bind them without converters): `IsRequestsSection`, `IsRunnerSection`,
    `IsLoadTestSection`, `IsMockServersSection`, `IsMonitorsSection` (`Section == ...`), `IsCollectionsSelected`
    (`IsRequestsSection && Mode == SidebarMode.Collections`) and `IsHistorySelected` (`IsRequestsSection && Mode == SidebarMode.History`).
    Add `[NotifyPropertyChangedFor(nameof(IsCollectionsSelected))]` and `IsHistorySelected` on the existing `_mode` field.
  - `ShowCollections` and `ShowHistory` also set `Section = ShellSection.Requests`.
  - `RequestRunner()`, `RequestLoadTest()`, `RequestMockServers()`, `RequestMonitors()`: when attached, set `Section` to the matching value,
    **then** raise the existing event (transitional: the views still open the tool windows until M6-78/M6-80; M6-81 removes the events).
    The four folder commands and the palette entries go through these methods already: no change there.
  - Commands for the rail: `ShowRunnerCommand`, `ShowLoadTestCommand`, `ShowMockServersCommand`, `ShowMonitorsCommand`
    (`[RelayCommand(CanExecute = nameof(CanAutomate))]`, each calls the matching `Request*` method). `AttachAutomation` calls
    `NotifyCanExecuteChanged` on them and also raises `PropertyChanged` for `Runner`, `LoadTest`, `Mocks` and `Monitors`.
  - Back to the requests: subscribe in the constructor to the new `Tabs.TabShown` and set `Section = ShellSection.Requests`.
  - Close: at the very start of `RequestCloseAsync`, when `Runner?.IsRunning == true || LoadTest?.IsRunning == true`,
    `await _dialogs.ConfirmAsync(Strings.ShellCloseRunningTitle, Strings.ShellCloseRunning)`; false returns `false` at once (nothing saved,
    nothing disposed). Otherwise continue with the unsaved-tab prompts unchanged. Running mock servers and monitors do not count: closing
    stops the mock servers silently, as today (ADR 0023).
- `TabsViewModel`: `public event EventHandler? TabShown;` ("Raised when a tab is opened or brought to the front on the user's request."),
  raised at the end of `NewTab`, `OpenAsNew`, `Open` (also when the tab was already open and active) and `Step` when it changed the
  active tab. Not raised by `CloseAsync`, `CloseWithoutPrompt` or setting `ActiveTab` directly.
- Strings (EN and FR, then regenerate `Strings.g.cs` as in `docs/personalization.md`):
  `shell.closeRunningTitle` = "Close Yaapic?" / "Fermer Yaapic ?";
  `shell.closeRunning` = "A collection run or a load test is still in progress. Closing Yaapic stops it and its results are lost." /
  "Une exécution de collection ou un test de charge est encore en cours. Fermer Yaapic l'arrête et ses résultats sont perdus.".

## Out of scope

The views (M6-77 to M6-80), removing the events (M6-81), saving the section in the session.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` (shell built as in `AutomationShellTests`):
  - default: `Section == Requests`, `IsRequestsSection` and `IsCollectionsSelected` true, the four tool bools false;
  - not attached: the four `Show*Command`s cannot execute and `Request*` leaves `Section` unchanged;
  - attached: each `Show*Command` sets its section, raises `PropertyChanged` for `Section` and the bools, and still raises its event;
  - each folder command (`RunFolderCommand`, `LoadTestFolderCommand`, `MockFromFolderCommand`, `MonitorFolderCommand`) sets its section
    and keeps its pre-selection (Runner and Load test loaded with the folder, a server generated, a monitor created);
  - each palette entry (`run-collection`, `load-test`, `mock-servers`, `monitors`) sets its section;
  - from `Runner`: `ShowCollectionsCommand` gives `Requests` + `IsCollectionsSelected`; `ShowHistoryCommand` gives `Requests` +
    `IsHistorySelected`; `Tabs.NewTab()`, `Tabs.Open(...)` of the already active request, `Tabs.OpenAsNew(...)` and `NextTabCommand`
    (two tabs) give `Requests` and keep the right active tab; `Tabs.CloseWithoutPrompt(...)` leaves `Runner`;
  - close: nothing running -> no `ConfirmAsync` call; only a started mock server -> no `ConfirmAsync` call and the close proceeds as
    before; a run in progress (hold it open the way the M6-21 tests do) -> `ConfirmAsync` called once with `Strings.ShellCloseRunning`; a
    load test in progress (held open the same way) -> same; answer false -> `RequestCloseAsync` returns false and the owned resources are
    not disposed; answer true -> it proceeds as before.
- [x] `TabsViewModelTests`: `TabShown` raised by `NewTab`, `OpenAsNew`, `Open` (new and already open), `NextTabCommand` with two tabs; not
  raised by `CloseAsync`, `CloseWithoutPrompt`, `NextTabCommand` with one tab.
- [x] Localization tests green (keys in both files, `Strings.g.cs` fresh); architecture tests green; 0 warnings, XML docs, all tests green.
- [x] Both front-ends still build; nothing visible changes yet.

## Suggested commit message

`feat(viewmodels): add shell sections for the automation views`
