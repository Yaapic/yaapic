# M6-56: Avalonia: automation entry points and status bar items

Status: todo
Platform: unix
Depends on: M6-49, M6-52, M6-53, M6-54, M6-55

## Goal

The Avalonia counterpart of M6-49: folder context menu, window handlers and status bar items.

## Design decisions

- Read first: M6-49 (items and behaviour to match), M6-41, M5-64 (how import/export entry points were added to the Avalonia context menu
  and `MainWindow.axaml.cs`), `src/Yaapic.App.Avalonia/Views/StatusBarView.axaml` (or the status bar part of `MainWindow.axaml`), M6-50
  (`ToolWindowHost.ShowOrActivate`).
- Context menu of every folder row: separator, Run folder..., Load test..., Create a mock server..., Monitor folder... bound to the shell
  commands with the node.
- `MainWindow.axaml.cs`: the four `*Requested` handlers open or activate their tool window (`RunnerView`, `LoadTestView`, `MockServersView`,
  `MonitorsView`) with the shell's view model; unsubscribe with the other handlers; close the tool windows after the main window's close is
  confirmed.
- Status bar: the Runner item bound to `RunnerStatusText`, the Mocks and Monitors items (hidden when empty, monitors in the error brush);
  clicking opens the matching window. Remove any temporary debug call left by M6-50 to M6-55.

## Out of scope

Keyboard shortcuts.

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-49; if not possible, say so in Notes and tick only the build and AOT items.
- [ ] Strings exist in EN and FR (M6-41); 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the automation entry points and status bar items`
