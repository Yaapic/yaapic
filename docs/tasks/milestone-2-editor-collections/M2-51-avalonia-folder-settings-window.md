# M2-51: Avalonia folder and collection settings window

Status: todo
Platform: unix
Depends on: M2-43, M2-39

## Goal

Same as M2-48, in `Yaapic.App.Avalonia`; read M2-48 first for behaviour and texts.

## Design decisions

- "Settings" item on the tree context menu (M2-39) calls `Collections.EditSettingsCommand`; `MainWindow` opens
  `Views/FolderSettingsWindow.axaml` (modal `Window`, compiled bindings, `x:DataType` = `FolderSettingsViewModel`) when
  `Collections.SettingsEditor` becomes non-null and calls `CloseSettingsCommand` when the window closes.
- Content as M2-48: name, description, the "Collection variables" section only when `ShowVariables` (reusing
  `KeyValueEditorView`), "Save", `LastError` banner.

## Out of scope

Same as M2-48.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-48 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add folder and collection settings window`
