# M1-31: Avalonia environment management

Status: done
Platform: unix
Depends on: M1-29

## Goal

Same environment management as M1-27, in `Yaapic.App.Avalonia`.

## Design decisions

- Behaviour and texts are those of M1-27; read it first.
- `Views/EnvironmentsWindow.axaml`: a modal `Window` (`ShowDialog`) instead of a `ContentDialog`, compiled bindings,
  `x:DataType` = `EnvironmentsViewModel`, reusing `KeyValueEditorView` from M1-29.
- Delete confirms with a `Flyout` and calls `DeleteConfirmedAsync`, as in M1-27 (keeps both apps identical).

## Out of scope

Same as M1-27.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M1-27, with the workspace under `~/Yaapic/Default`.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add environment management window`

## Implementation notes

- The manual smoke (add "Local", save, send, restart, delete under `~/Yaapic/Default`) was not run, and macOS/Linux
  were not available: the window was only checked to build, publish with AOT (0 IL2xxx/IL3xxx warnings) and start on
  Windows without crashing.
- Opened from an "Environments" button and Ctrl/Cmd+Shift+E in the main window.
