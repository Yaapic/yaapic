# M2-48: WinUI folder and collection settings dialog

Status: done
Platform: windows
Depends on: M2-43, M2-37

## Goal

Edit a folder's name and description and a collection's variables from the WinUI sidebar.

## Design decisions

- New menu item "Settings" on the folder/collection context menu (M2-37), calling `Collections.EditSettingsCommand` with
  the node. The window shows `Views/FolderSettingsDialog.xaml` (`ContentDialog`, close button "Close") whenever
  `Collections.SettingsEditor` becomes non-null (subscribe to its property change in `MainWindow` code-behind) and calls
  `Collections.CloseSettingsCommand` when the dialog closes. Only one `ContentDialog` can be open: do not open it while
  another is up.
- Dialog (`x:DataType` = `FolderSettingsViewModel`): `TextBox` for `Name`, multi-line `TextBox` for `Description`
  (placeholder "What is this collection for?"), then, only when `ShowVariables`, a section "Collection variables" with the
  hint "Override globals; the active environment overrides them." and a `KeyValueEditorView` (headers "Variable" /
  "Value"); a "Save" button on `SaveCommand`; `LastError` in an `InfoBar`.
- Use the dialog and input styles of M1-37; spacing as the environments dialog.

## Out of scope

Collection auth, headers or scripts.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: in collection "Demo" add variable `host = https://httpbin.org`, save; a request in
      that collection sending `{{host}}/get` returns 200, the same request moved to the top level shows the variable
      unresolved; renaming the collection renames its directory on disk.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add folder and collection settings dialog`

## Notes

- Verified on the AOT-published exe (win-x64, 0 IL2xxx/IL3xxx): created collection "Demo", set `host = https://httpbin.org`
  and renamed it to "Demo2" in the dialog; `requests/demo2/folder.yaml` holds the variable and the directory was renamed.
  `{{host}}/get` showed "Unresolved variables: host" at the top level and returned 200 OK after moving into the collection.
- Every theme key used by the dialog is already used by `EnvironmentsDialog` and exists in `Yaapic.Colors.WinUI.xaml`.
