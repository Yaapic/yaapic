# M2-24: Avalonia response copy and save buttons

Status: done
Platform: unix
Depends on: M2-14, M2-20, M1-57

## Goal

Same as M2-19, in `Yaapic.App.Avalonia`; read M2-19 first for behaviour and texts.

## Design decisions

- In `Views/ResponsePanelView.axaml` (or the response part of `RequestEditorView.axaml`, wherever the Pretty/Raw control
  of M1-30 lives): two icon `Button`s with `ToolTip.Tip` "Copy response body" and "Save response to file", `Command`
  bound to `CopyBodyCommand` and `SaveBodyCommand`; icons from `design/xaml/Yaapic.Icons.Avalonia.axaml` when one fits.

## Out of scope

Same as M2-19.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke: same checks as M2-19 on the host OS.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): copy and save the response body`
