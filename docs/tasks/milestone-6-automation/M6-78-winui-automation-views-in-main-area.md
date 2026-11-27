# M6-78: WinUI: automation views in the main area, tool windows removed

Status: todo
Platform: windows
Depends on: M6-77

## Goal

On WinUI the Runner, Load test, Mock servers and Monitors views show in the main window in place of the request area (ADR 0023); every
entry point (rail, folder context menu, palette, status bar) goes there; the tool window host is deleted. This replaces the window
behaviour of M6-43 and M6-49 (those done cards are not edited).

## Design decisions

- Read first: ADR 0023, M6-76, M6-77, `src/Yaapic.App.WinUI/MainWindow.xaml` (row 2: rail column, sidebar column, main `Grid Grid.Column="2"`
  with the `TabView` and the empty state) and `MainWindow.xaml.cs` (the `ViewModel` setter subscriptions, `OnRunnerRequested`..
  `OnMonitorsRequested`, the four `_...Window` fields, `Closed += (_, _) => Views.ToolWindow.CloseAll();`, `OnCloseTab`, `OnSaveTab`,
  `ThemeRoot`), `Views/ToolWindow.cs`, `Services/WinUIDialogService.cs` (`ActiveWindow`).
- The four views are already `UserControl`s with a `ViewModel` dependency property and no window-specific code; **their XAML and
  code-behind do not change**, except a layout fix found by the smoke at 900 x 600 (see below).
- `MainWindow.xaml`, row 2:
  - Sidebar column: `<ColumnDefinition Width="Auto"/>` instead of `272`; the sidebar `Grid` (column 1) gets `Width="272"` and
    `Visibility="{x:Bind p:VisibilityMap.WhenTrue(ViewModel.IsRequestsSection), Mode=OneWay}"`.
  - Main area (column 2): wrap the existing `TabView` and the empty-state `StackPanel` in a `Grid` with the same `IsRequestsSection`
    visibility (the empty state keeps its own `Tabs.IsEmpty` binding inside). After that `Grid`, add the four views, created once and only
    hidden (never `x:Load`, so their state survives):
    `<v:RunnerView ViewModel="{x:Bind ViewModel.Runner, Mode=OneWay}" AutomationProperties.Name="{x:Bind loc:Strings.RunnerTitle}" Visibility="{x:Bind p:VisibilityMap.WhenTrue(ViewModel.IsRunnerSection), Mode=OneWay}"/>`,
    the same for `LoadTestView`/`LoadTest`/`LoadTitle`/`IsLoadTestSection`, `MockServersView`/`Mocks`/`MockTitle`/`IsMockServersSection`,
    `MonitorsView`/`Monitors`/`MonitorTitle`/`IsMonitorsSection`. No extra header: the rail shows where the user is.
- `MainWindow.xaml.cs`:
  - Delete the four `_...Window` fields, the four `On...Requested` automation handlers and their `+=`/`-=` lines, and the `Closed += ...
    ToolWindow.CloseAll()` line. `ThemeRoot` (and anything else only `ToolWindow` used): delete when no other caller remains.
  - `OnCloseTab` and `OnSaveTab` (Ctrl+W, Ctrl+S): do nothing unless `_viewModel is { IsRequestsSection: true }` (still set
    `args.Handled = true`). Ctrl+T and Alt+Up/Down need nothing: the tabs view model raises `TabShown` (M6-76).
- Delete `Views/ToolWindow.cs`. `WinUIDialogService`: delete `ActiveWindow`; dialogs use `_xamlRoot()` and pickers `_window`, as before
  M6-43. `LineChart` stays.
- The status bar, the folder context menu and the palette need no view change: they call the shell, which now sets `Section`; the
  shell's `*Requested` events still fire until M6-81 but nothing subscribes on WinUI.
- Layout at the minimum window size (900 x 600): the tool gets 852 dip (rail 48), more than the 800 of the old tool windows. If a view
  still clips, fix it in the view with the smallest change (a `MinWidth`, a `ScrollViewer`, a narrower fixed column) and say so in Notes.

## Out of scope

Avalonia (M6-79, M6-80); removing the shell events (M6-81); docs and screenshots (M6-82, M6-83).

## Acceptance criteria

- [ ] `rtk grep -rn "ToolWindow\|ActiveWindow" src/Yaapic.App.WinUI --include=*.cs --include=*.xaml` finds nothing (outside `obj/`).
- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [ ] Manual smoke on the published exe (UI Automation or by hand; record each item PASS / not run in Notes):
  - each rail entry shows its view in the main area, hides the sidebar and the request area, highlights the entry; there is never a
    second top-level window;
  - Collections and History bring back the sidebar and the request area with the same active tab;
  - each folder context-menu item (Run folder..., Load test..., Create a mock server..., Monitor folder...) shows its view with the folder
    pre-selected (Runner and Load test list its requests, the generated server is selected, the new monitor is selected);
  - the palette entries and the status bar items (Runner, Mocks, Monitors) show their view;
  - start a Runner run with a delay, go to Collections, open and send a request, come back: the run went on (status bar progress) and
    the results and selection are there; a started mock server keeps answering `curl http://127.0.0.1:<port>/...` while another view is
    shown;
  - the load-test confirmation and the Runner CSV picker open over the main window;
  - from a tool, Ctrl+T shows the requests with a new tab; Ctrl+W and Ctrl+S do nothing;
  - closing with a running mock server asks "Close Yaapic?": Cancel keeps Yaapic open, Continue exits and the port stops answering;
    closing with nothing running asks nothing;
  - at 900 x 600 every view is usable (nothing clipped without a scroll bar); dark and light look right.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): show the automation views in the main window`
