# M6-49: WinUI: automation entry points and status bar items

Status: todo
Platform: windows
Depends on: M6-45, M6-46, M6-47, M6-48

## Goal

The user reaches the Runner, Load test, Mock servers and Monitors from the folder context menu, the palette and the status bar.

## Design decisions

- Read first: M6-41 (shell events, folder commands, status texts, strings), M5-59 (how the import/export entry points were added to the
  context menu and `MainWindow.xaml.cs`), `Views/StatusBarView.xaml(.cs)` (the placeholder items of the mockup footer: `Console`, `Cookies`,
  `Runner`...), M6-43 (`ToolWindow.ShowOrActivate`).
- **Context menu** of every folder row (any depth): a separator then `Strings.CollectionsRunFolder`, `Strings.CollectionsLoadTest`,
  `Strings.CollectionsCreateMock`, `Strings.CollectionsMonitorFolder`, bound to the four shell commands with the node.
- **Handlers** in `MainWindow.xaml.cs`: `RunnerRequested` -> `ToolWindow.ShowOrActivate(ref _runnerWindow, Strings.RunnerTitle,
  () => new RunnerView { ViewModel = shell.Runner }, this)`; the same for `LoadTestRequested` (`LoadTestView`), `MockServersRequested`
  (`MockServersView`) and `MonitorsRequested` (`MonitorsView`). Unsubscribe with the other handlers.
- **Status bar**: the `Runner` placeholder becomes a button bound to `RunnerStatusText` (opens the Runner); add `MocksStatusText` and
  `MonitorsStatusText` buttons after it, collapsed when empty (`VisibilityMap`), the monitors one in the error brush; clicking opens the
  matching window.
- The palette entries need no view work (M6-41). Remove any temporary debug call left by M6-43 to M6-48.

## Out of scope

Keyboard shortcuts (none defined).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [ ] Manual smoke on the published exe: each context-menu item opens the right window loaded with the folder; the palette entries open
  them; a second request activates the open window instead of a new one; the Runner status shows progress during a run; the Mocks item
  appears while a server runs; the Monitors item appears after a failing run and opens the window; closing the main window closes the tool
  windows and stops mock servers. Record anything not run in Notes.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the automation entry points and status bar items`
