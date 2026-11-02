# M2-59: Avalonia sidebar mode switch and history list

Status: todo
Platform: unix
Depends on: M2-57, M2-38

## Goal

Same as M2-58, in `Yaapic.App.Avalonia`; read M2-58 first for behaviour and texts.

## Design decisions

- Rail (or the existing sidebar header of `MainWindow.axaml`, whichever M1-28 built) gets "Collections" and "History"
  toggle buttons bound to `ShowCollectionsCommand` / `ShowHistoryCommand`, selected state from `Mode` through converters.
- `Views/HistoryView.axaml` (compiled bindings, `x:DataType` = `HistoryViewModel`): filter `TextBox`, "Clear" button, an
  `ItemsControl` of groups (header + nested `ItemsControl` of items), item template as in M2-58 with
  `BrushMap.MethodBrush` and a status chip, `ContextMenu` Open/Delete, empty state text, `LastError` in the existing banner.

## Out of scope

Same as M2-58.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-58 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add sidebar mode switch and history list`
