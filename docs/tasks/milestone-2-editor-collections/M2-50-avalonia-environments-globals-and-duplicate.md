# M2-50: Avalonia environments window: globals and duplicate

Status: done
Platform: unix
Depends on: M2-42, M2-45, M1-57

## Goal

Same as M2-47, in `Yaapic.App.Avalonia`; read M2-47 first for behaviour and texts.

## Design decisions

- `Views/EnvironmentsWindow.axaml` (compiled bindings): a fixed "Globals" row above the environments list bound to
  `Environments.IsEditingGlobals` (selection) and `Globals.IsDirty`; the right side swaps between the globals editor
  (`KeyValueEditorView` on `Globals.Variables`, "Save" on `Globals.SaveCommand`, `Globals.LastError` banner, the same
  explanatory text) and the environment editor via `IsVisible`; "Duplicate" button on `Environments.DuplicateCommand`.
- The window needs both view models: give it the shell (or a small holder) as `DataContext` with `x:DataType` updated
  accordingly; keep the Flyout delete confirmation of M1-31.

## Out of scope

Same as M2-47.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke: same checks as M2-47 on the host OS (Windows, published AOT exe driven through UI Automation: global
      `gbase` saved to `globals.yaml`, `{{gbase}}/users` sent and hit the stub at the global's value; "staging" duplicated
      to "staging copy", renamed and saved: both env files exist; the environment-wins check is covered by the VariableResolver tests).
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): edit globals and duplicate environments`
