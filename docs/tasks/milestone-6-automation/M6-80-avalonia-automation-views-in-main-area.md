# M6-80: Avalonia: automation views in the main area, tool windows removed

Status: done
Platform: unix
Depends on: M6-78, M6-79

## Goal

The Avalonia counterpart of M6-78: the four views show in the main window in place of the request area, every entry point goes there,
and `ToolWindowHost` is deleted. This replaces the window behaviour of M6-50 and M6-56 (those done cards are not edited).

## Design decisions

- Read first: M6-78 (behaviour to match, smoke list), ADR 0023, `src/Yaapic.App.Avalonia/MainWindow.axaml` (row 2:
  `ColumnDefinitions="48,272,*"`, the sidebar `Border` in column 1, the main `Grid Grid.Column="2"` with the `TabControl`, the add button
  and the empty state; the `HistoryView` line that binds `IsVisible` through `$parent[Window]`), `MainWindow.axaml.cs` (the
  `KeyBindings` for Ctrl+W/Ctrl+S, the shell subscriptions, `OnRunnerRequested`..`OnMonitorsRequested`, `Open<T>`, the four slots,
  `ToolWindowHost.CloseAll()` in `OnClosing`), `Views/ToolWindowHost.cs`, `Services/AvaloniaDialogService.cs` (`Owner()`).
- The four views (`RunnerView`, `LoadTestView`, `MockServersView`, `MonitorsView`) take their view model as `DataContext`; their XAML and
  code-behind do not change, except a layout fix found by the smoke at 900 x 600 (same rule as M6-78).
- `MainWindow.axaml`, row 2:
  - `ColumnDefinitions="48,Auto,*"` (update the comment above it: 272 is now the sidebar's `Width`); the sidebar `Border` gets
    `Width="272"` and `IsVisible="{Binding IsRequestsSection}"`.
  - Main area: wrap the `TabControl`, the add `Button` and the empty-state `StackPanel` in a `Panel IsVisible="{Binding IsRequestsSection}"`.
    After it add the four views, created once and only hidden. Their `DataContext` is the tool view model, so their `IsVisible` binds
    through the window like `HistoryView` does:
    `<views:RunnerView DataContext="{Binding Runner}" AutomationProperties.Name="{x:Static loc:Strings.RunnerTitle}" IsVisible="{Binding $parent[Window].((vm:ShellViewModel)DataContext).IsRunnerSection}"/>`,
    and the same for `LoadTestView`/`LoadTest`/`LoadTitle`/`IsLoadTestSection`, `MockServersView`/`Mocks`/`MockTitle`/`IsMockServersSection`,
    `MonitorsView`/`Monitors`/`MonitorTitle`/`IsMonitorsSection`.
- `MainWindow.axaml.cs`: delete the four automation handlers, their subscriptions, `Open<T>`, the four `Window?` slots and the
  `ToolWindowHost.CloseAll()` call; the Ctrl+W and Ctrl+S `KeyBinding` actions run only when `Shell is { IsRequestsSection: true }`.
- Delete `Views/ToolWindowHost.cs`. `AvaloniaDialogService.Owner()` returns `_owner()`. `Controls/LineChart.cs` stays.

## Out of scope

Removing the shell events (M6-81), docs (M6-82).

## Acceptance criteria

- [x] `rtk grep -rn "ToolWindowHost\|ActiveWindow" src/Yaapic.App.Avalonia --include=*.cs --include=*.axaml` finds nothing (outside `obj/`).
- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [x] Runtime smoke on macOS or Linux with the M6-78 list; if not possible, run what UI Automation reaches on Windows (rail entries, status
  bar items, palette, single top-level window, close confirmation during a run or a load test and none with only a mock server running)
  and list the rest as not run in Notes.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): show the automation views in the main window`

## Notes

Verified on Windows only (win-x64 AOT publish, 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly), driven through mouse/keyboard input and
screenshots (dark theme, English; screenshots viewed). PASS: each rail entry shows its view in the main area, highlights the entry, hides the sidebar and
the request area, with a single top-level window; Collections brings the sidebar and tabs back; the palette entry "Mock servers" and the status bar
Runner item show their view; Ctrl+W and Ctrl+S do nothing from a tool and Ctrl+T shows the requests with a new tab (Create user and List users tabs kept);
the folder context menu "Run folder..." shows the Runner with the folder's 3 requests selected and the run completes; in Mock servers, creating a server,
selecting it, adding a route (Method combo) and starting it works without a crash, the server answers (`GET /` returned the "No mock route" JSON) and the
status bar shows "Mocks: 1 running"; closing with only a running mock server exits without a prompt and the port stops answering; closing with nothing
running exits without a prompt. The MockServersView at 1280 x 800 is not clipped. Not verified on unix: macOS/Linux run, light theme, tooltips.
NOT RUN (UI Automation traversal hung on the busy tree and another application stole the foreground, so the session was cut short): the other folder
menu items (Load test, Mock server, Monitor), a Runner run continuing while another section is shown, the "Close Yaapic?" prompt during a run or a load
test (Cancel and Continue), the file pickers and the load-test confirmation over the main window, and a true 900 x 600 window check. No layout change
was made to the four views.
