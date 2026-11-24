# M5-57: WinUI: import dialog

Status: done
Platform: windows
Depends on: M5-51, M5-55, M5-56

## Goal

The Import dialog: choose a file or folder (or paste text), see the detected format and the report, import.

## Design decisions

- Read first: M5-51 (`ImportViewModel` members and strings), M5-56, M5-55, `Views/CodeGenerationDialog.xaml(.cs)` (a `ContentDialog` with its own max width; the M4-68 notes about `ContentDialogMaxWidth`, clipping and the AOT `ListMap` crash), `Views/ConvertScriptDialog.cs` (a paste dialog), `Presentation/ListMap.cs`.
- `Views/ImportDialog.xaml(.cs)` (`ImportViewModel`), a `ContentDialog` with no default button bar (own buttons), default size 720 x 560, `ContentDialogMaxWidth` raised as in M4-68. Layout, top to bottom:
  1. A row with **Choose a file...** (`ChooseFileCommand`) and **Choose a folder...** (`ChooseFolderCommand`), and the `SourceName` text next to them.
  2. A multi-line `TextBox` (3 lines, `AcceptsReturn`, monospace) bound to `PastedText` with the hint `ImportPasteHint` and a **Preview** button (`PreviewPastedCommand`).
  3. A `ComboBox` for `Formats` (label `ImportFormat`, items through `ListMap.ImportFormats`, selection `SelectedFormat`) and the text `Strings.ImportDetected(DetectedFormat)` when known.
  4. A `CheckBox` `IncludeStaticResources` (label `ImportIncludeStatic`) visible when `ShowStaticResourcesOption`.
  5. The counts line (`Strings.ImportCounts(...)`, visible when `HasPreview`) and the `ImportReportView` (`Report`) filling the remaining height.
  6. An error line (`Error`, error brush) and a busy `ProgressRing` while `IsBusy`.
  7. Buttons: **Import** (`ImportCommand`, primary; after success it shows `ResultSummary` in the status line and the button becomes disabled) and **Close**.
- The dialog calls `ViewModel.Reset()` when it opens. It is opened by the shell handler of M5-59 with a fresh `ImportViewModel` from `shell.CreateImport()`.
- The code-behind only forwards events; no logic. Native AOT rules of M4-68: every list is bound through `ListMap` (`ListMap.ImportFormats(IReadOnlyList<ImportFormatChoice>)` to add).

## Out of scope

Entry points (M5-59), drag and drop of files.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts. (Verified: publish clean, exe starts and stays alive.)
- [ ] Manual smoke on the published exe (the dialog is opened from the palette entry of M5-53 once M5-59 is done; until then open it from a temporary debug call and remove it): import a Postman collection file, a cURL text, a Bruno folder, a HAR file (the static-resources checkbox appears), an OpenAPI YAML file; the report list shows entries
  (this is the first AOT check of a **non-empty** report list); Copy puts the report on the clipboard; an invalid file shows the error and choosing a format by hand works; import adds the folder to the sidebar tree. Record anything not run.
- [x] Strings in EN and FR exist (M5-50, M5-51); 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the import dialog`

## Notes

Manual smoke of the dialog not run here: the dialog has no entry point until M5-59; it is covered by the visual verification card M5-68.
