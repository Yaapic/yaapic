# M5-51: ViewModel: import

Status: todo
Platform: any
Depends on: M5-10, M5-49, M5-50

## Goal

`ImportViewModel` drives the import dialog: choose a file, a folder or pasted text, preview the report and counts, import.

## Design decisions

- Read first: ADR 0016 ("Import flow", UI), M5-10 (`ImportService`, `ImportPreview`, `IImportInputReader`), M5-50, M5-49, `src/Yaapic.ViewModels/Shell/ShellViewModel.cs` (`ReloadAsync`, `AttachTools`), a dialog-like view model such as `CertificatesViewModel`.
- `src/Yaapic.ViewModels/Import/ImportViewModel.cs`, constructor `(ImportService service, ImporterRegistry registry, IImportInputReader reader, IDialogService dialogs, Func<Task> reloadWorkspace)`.
  - **Formats**: `IReadOnlyList<ImportFormatChoice> Formats` = `Auto` first (`Id` null, `Strings.ImportFormatAuto`) then one per registered importer (`Id`, `DisplayName`); `SelectedFormat` (default Auto).
  - **Input**: `SourceName` (file or folder name, or `Strings.ImportPastedText`), `PastedText` (string); `ChooseFileCommand` (`dialogs.PickFileAsync(Strings.ImportChooseFileTitle)`, then `reader.ReadFileAsync`), `ChooseFolderCommand` (`PickFolderAsync(Strings.ImportChooseFolderTitle)`, `ReadFolderAsync`),
    `PreviewPastedCommand` (enabled when `PastedText` is not blank; `ImportInput.FromText(Strings.ImportPastedText, PastedText)`). A canceled picker changes nothing. An `ImportException` from the reader sets `Error` (the exception message) and clears the preview.
  - **Preview**: after any input, a change of `SelectedFormat` or of `IncludeStaticResources` (bool, default false), run `service.Preview(input, SelectedFormat.Id, new ImportOptions(IncludeStaticResources))` on a background task (`Task.Run`, the parsing can be slow) with cancellation of the previous run;
    results: `DetectedFormat` (importer display name, or null), `Folders`, `Requests`, `Environments`, `GlobalVariables` (from the bundle: `FolderCount` minus 0, `RequestCount`, `Environments.Count`, `Globals.Count`), `Report` (`ImportReportViewModel`, loaded with the bundle report), `HasPreview`.
    An `ImportException` (also "not recognized") sets `Error` and clears `HasPreview`; the user can then pick a format by hand. `ShowStaticResourcesOption` is true when the detected importer id is `har`.
  - **Import**: `ImportCommand` (async, enabled when `HasPreview` and not busy and the bundle has at least one request, environment or global): `service.ApplyAsync`, then `await reloadWorkspace()`, then `Report` is replaced by the **outcome** report, `Outcome` (the `ImportOutcome`) is set, `IsDone` true, `ResultSummary` = `Strings.ImportResult(requests, folders, environments)`. A `StorageException` or other failure sets `Error`.
  - State properties: `IsBusy`, `Error` (string?), `HasError`, `IsDone`. `void Reset()` clears everything (the dialog calls it when it opens).
  - Raises `Imported` (`EventHandler<ImportOutcome>`) after a successful apply.
- Strings (EN and FR): `import.title` = "Import"; `import.formatAuto` = "Detect automatically"; `import.format` = "Format"; `import.pastedText` = "Pasted text"; `import.chooseFileTitle` = "Choose a file to import"; `import.chooseFolderTitle` = "Choose a Bruno collection folder"; `import.chooseFile` = "Choose a file..."; `import.chooseFolder` = "Choose a folder...";
  `import.pasteHint` = "...or paste a cURL command or the content of a file"; `import.preview` = "Preview"; `import.includeStatic` = "Include images, fonts and styles"; `import.import` = "Import"; `import.close` = "Close"; `import.counts` = "{0} folders, {1} requests, {2} environments, {3} global variables"; `import.result` = "Imported {0} requests in {1} folders and {2} environments.";
  `import.detected` = "Detected: {0}"; `import.busy` = "Reading...".

## Out of scope

The dialogs, palette entries (M5-53), composition (M5-54).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests/Import` with fake importers, `InMemory*` stores, `FakeDialogService` and a fake `IImportInputReader`: Auto lists importers; choose file (preview counts and report, detected name); canceled picker; reader exception sets `Error`; pasted text preview; unrecognized text sets `Error`, then choosing a format by hand previews;
  toggling `IncludeStaticResources` re-previews and `ShowStaticResourcesOption` only for `har`; import applies, calls `reloadWorkspace`, replaces the report with the outcome report, sets `IsDone`/`ResultSummary`, raises `Imported`; import disabled with an empty bundle; `Reset`; a newer preview cancels the older one (use a gated fake importer).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the import view model`
