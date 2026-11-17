# M5-58: WinUI: export dialog

Status: todo
Platform: windows
Depends on: M5-52, M5-55, M5-56

## Goal

The Export dialog: choose a collection and a format, export to a file chosen in the save dialog, read the report.

## Design decisions

- Read first: M5-52 (`ExportViewModel` members and strings), M5-56 (`ImportReportView`), M5-57 (dialog layout and AOT list binding), the M4-68 notes.
- `Views/ExportDialog.xaml(.cs)` (`ExportViewModel`), a `ContentDialog` 640 x 520:
  1. `ComboBox` **Collection** (`Collections`, `SelectedCollection`; through `ListMap.ExportChoices`), hidden when the selected format does not need a collection; `Strings.ExportNoCollections` when the list is empty.
  2. `ComboBox` **Format** (`Formats`, `SelectedFormat`; `ListMap.ExportFormats`).
  3. For `postman-environment` (`ShowEnvironments`): a `ListView` with check boxes (`EnvironmentChoices`, `IsSelected`; `ListMap.ExportEnvironmentChoices`, items are observable) and the `IncludeGlobals` check box.
  4. The one-line note `Strings.ExportSecretsNote`.
  5. After an export: `Strings.ExportSaved(SavedName)` and the `ImportReportView` (`Report`), the error line, a `ProgressRing` while `IsBusy`.
  6. Buttons: **Export...** (`ExportCommand`, primary; opens the save picker through the dialog service) and **Close**.
- The dialog is opened by the shell handler of M5-59 with `shell.CreateExport()` then `LoadAsync(collectionId)` where the id comes from the `ExportRequested` event argument.
- The WinUI save picker already exists behind `IDialogService.SaveFileAsync`; nothing to add there. The file type choice of the picker is derived from the extension of the suggested name (check how `SaveFileAsync` does it; a `.zip` and a `.json` name must both work: if it only offers one extension, extend it in this card).

## Out of scope

Entry points (M5-59).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [ ] Manual smoke on the published exe: export a collection as Postman JSON, OpenAPI YAML, Insomnia, Bruno zip; the save dialog proposes the right name and extension; the exported files open in a text editor (check the JSON); the Postman environment format shows the environment list and globals box; secrets note visible; the report appears after the export. Record anything not run.
- [ ] Strings in EN and FR exist (M5-52); 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the export dialog`
