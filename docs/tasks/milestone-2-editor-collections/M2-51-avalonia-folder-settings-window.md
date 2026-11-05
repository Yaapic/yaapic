# M2-51: Avalonia folder and collection settings window

Status: done
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

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke: same checks as M2-48 on the host OS.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add folder and collection settings window`

## Notes

- AOT publish (win-x64): 0 IL2xxx/IL3xxx; the only ILC messages are Avalonia.Win32's own `UnmanagedCallersOnly` notices.
- Smoke on the published exe (Windows host, UI Automation): right-click the collection, "Settings" opens the modal
  `FolderSettingsWindow` centred inside the main window, below its title bar (height capped to the owner, body scrolls);
  the fixture variable `team = platform` is listed; renaming to "Users API 2" and Save renamed `requests/users-api` to
  `requests/users-api-2`. Closing the window clears `Collections.SettingsEditor`. A dark-theme capture
  (`Capture-Avalonia.ps1 -Open settings`, new `settings` name) matches the WinUI layout.
- The variable popup's Collection scope now opens this window for the active tab's collection (as on WinUI).
- Not run: the `{{host}}/get` request round-trip against httpbin (network); variable resolution is shared ViewModel code
  already covered by M2-43 and verified on WinUI in M2-48.
