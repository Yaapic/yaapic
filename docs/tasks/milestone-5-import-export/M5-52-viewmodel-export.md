# M5-52: ViewModel: export

Status: done
Platform: any
Depends on: M5-36, M5-37, M5-49

## Goal

`ExportViewModel` drives the export dialog: choose a collection and a format, produce the file or zip, save it.

## Design decisions

- Read first: ADR 0016 ("Export contract", UI), M5-36 (`ExportSourceReader`, `ExporterRegistry`), M5-37 (`ExportPackager`), M5-50 (`ImportReportViewModel`), `IDialogService.SaveFileAsync`, `src/Yaapic.ViewModels/Collections/CollectionTreeViewModel.cs` (how top-level folders are listed).
- `src/Yaapic.ViewModels/Import/ExportViewModel.cs`, constructor `(ExporterRegistry registry, ExportSourceReader reader, ICollectionStore collections, IEnvironmentStore environments, IDialogService dialogs)`.
  - **Load**: `Task LoadAsync(Guid? preselectedCollectionId)` loads the top-level folders (`Collections`: `ExportChoice(Id, Name)` sorted by name) and the environments (`EnvironmentChoices`: `ExportEnvironmentChoice(Id, Name, CollectionName?)` with `IsSelected` observable). `SelectedCollection` defaults to the preselected id, else the first one.
  - `Formats` = the exporters (`ExportFormatChoice(Id, DisplayName)`), `SelectedFormat` (default `postman`, or the first). `RequiresCollection` and `ShowEnvironments` are derived from the selected exporter: only `postman-environment` shows the environment list; the formats with a collection export the environments scoped to the selected collection automatically (except Postman collection and `openapi-*`, which export none). `IncludeGlobals` (bool, default false, shown for `postman-environment` only).
  - **Export**: `ExportCommand` (async; enabled when the selected exporter has what it needs: a collection when `RequiresCollection`, at least one selected environment or globals otherwise): builds the `ExportSource` with `reader.ReadAsync` (collection id when required; environment ids = for collection formats the environments whose `CollectionId` equals the collection (Postman: none, Insomnia: those;
    Bruno: those; OpenAPI: none), for `postman-environment` the selected ones; `IncludeGlobals`), runs `exporter.Export(source)`, packages with `ExportPackager.Package(result, source.Name, exporter.FileExtension)`, then `dialogs.SaveFileAsync(fileName, content)`. Cancelled save: no error, `Saved` false. The report (the reader's merged with the exporter's) is loaded into `Report` (`ImportReportViewModel`).
  - State: `IsBusy`, `Error` (an `ExportException` message or `Strings.ExportFailed(message)`), `Saved` (true after a successful save), `SavedName`. `ExportCommand` runs the export on a background task; saving happens on the caller context.
- Strings (EN and FR): `export.title` = "Export"; `export.collection` = "Collection"; `export.format` = "Format"; `export.environments` = "Environments"; `export.includeGlobals` = "Include global variables"; `export.export` = "Export..."; `export.saved` = "Saved {0}"; `export.failed` = "The export failed: {0}"; `export.close` = "Close";
  `export.noCollections` = "There is no collection to export yet."; `export.secretsNote` = "Secret values are never exported.".

## Out of scope

The dialogs, palette (M5-53), composition (M5-54).

## Acceptance criteria

- [x] Tests with fake exporters (one needing a collection, one not), in-memory stores and `FakeDialogService`: `LoadAsync` lists collections and environments, preselection; export of a collection format writes one file (name and bytes) through `SaveFileAsync`; several files produce a zip name; cancelled save leaves `Saved` false without an error;
  the environment exporter uses the selected environments and `IncludeGlobals`; the command is disabled without a collection / without environments; `ExportException` and unexpected exceptions set `Error`; secrets blanked (a secret row never reaches the exporter: assert on the `ExportSource` a fake exporter receives); the report is loaded (reader secrets `Info` + exporter entries).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the export view model`
