# M1-31: Avalonia environment management

Status: todo
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

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M1-27, with the workspace under `~/Yaapic/Default`.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add environment management window`
