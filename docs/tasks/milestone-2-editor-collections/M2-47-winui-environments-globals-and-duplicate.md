# M2-47: WinUI environments dialog: globals and duplicate

Status: done
Platform: windows
Depends on: M2-42, M2-45

## Goal

The WinUI environment manager edits the global variables and duplicates environments.

## Design decisions

- Read `Views/EnvironmentsDialog.xaml(.cs)` (M1-27, restyled in M1-43) and `design/mockups/EnvVars.dc.html` first.
- The dialog binds to `ViewModel.Environments` as today and to `ViewModel.Globals` (pass the shell or both view models to
  the dialog; `x:Bind` only).
- Left list: a fixed first row "Globals" (above the environments list, same row style as an environment row, no dot,
  dirty marker `•` from `Globals.IsDirty`), selected state from `Environments.IsEditingGlobals`; clicking it sets
  `IsEditingGlobals = true`; clicking an environment row sets `EditedEnvironment` as today.
- Right side: when `IsEditingGlobals`, the header shows "Globals" and the text "Available in every request; the active
  environment and the collection override them.", then `KeyValueEditorView` bound to `Globals.Variables` (headers
  "Variable" / "Value") and a "Save" button on `Globals.SaveCommand`; `Globals.LastError` in an inline `InfoBar`. Otherwise
  the environment editor as today.
- Environment header gets a "Duplicate" button (`Environments.DuplicateCommand` with the edited environment).
- Visibility through `VisibilityMap`/`EnvironmentMap` functions; keep the Flyout-based delete of M1-27.

## Out of scope

Secrets, import/export, per-collection editing here (that is the folder settings dialog).

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: add a global `baseUrl = https://httpbin.org`, save, send `{{baseUrl}}/get`: 200;
      `globals.yaml` exists in the workspace; define the same name in an environment and select it: the environment value wins;
      duplicate an environment, rename it, save: both files exist.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): edit globals and duplicate environments`
