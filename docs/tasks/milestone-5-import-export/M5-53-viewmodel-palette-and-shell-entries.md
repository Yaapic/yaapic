# M5-53: ViewModel: palette commands and shell entries for import and export

Status: todo
Platform: any
Depends on: M5-51, M5-52

## Goal

The shell offers Import and Export: events the views open dialogs from, palette entries, and the sidebar item.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Shell/ShellViewModel.cs` (`EnvironmentsRequested`, `JwtDecoderRequested`, `CertificatesRequested`, `AttachTools`, `ReloadAsync`), `src/Yaapic.ViewModels/Palette/CommandsPaletteSource.cs` (how the M4 tool entries are added) and its tests, M4-60 (the same card for milestone 4), M5-51, M5-52.
- `ShellViewModel`:
  - `event EventHandler? ImportRequested`, `void RequestImport()`; `event EventHandler<Guid?>? ExportRequested`, `void RequestExport(Guid? collectionId = null)`.
  - `void AttachImportExport(Func<ImportViewModel> import, Func<ExportViewModel> export)` (called once by the composition root; a second call throws `InvalidOperationException`, like `AttachPalette`), `ImportViewModel CreateImport()` and `ExportViewModel CreateExport()` (throw `InvalidOperationException` when not attached). `ImportRequested`/`ExportRequested` do nothing when not attached.
  - `CollectionTreeViewModel` (or its node view model) gets `ExportCollectionCommand` on top-level folder nodes raising `ExportRequested` through the shell: wire it as `ShellViewModel.RequestExport(node.Id)` from a `RelayCommand<CollectionNodeViewModel>` on the shell (`ExportCollectionCommand`), enabled only for top-level folders.
- `CommandsPaletteSource`: entries `import` (`Strings.PaletteImport`, no shortcut, runs `RequestImport`), `export-collection` (`Strings.PaletteExport`, runs `RequestExport(null)`), offered only when import and export are attached. When the active tab belongs to a collection (`_shell.Collections.FindCollection(activeTab.Id)`), the entry
  `export-this-collection` (`Strings.PaletteExportThisCollection(name)`) runs `RequestExport(collection.Id)`.
- Strings (EN and FR): `palette.import` = "Import a collection or request..."; `palette.export` = "Export a collection..."; `palette.exportThisCollection` = "Export collection \"{0}\"..."; `collections.import` = "Import..."; `collections.export` = "Export..." (the sidebar and context menu labels the views use).

## Out of scope

Dialogs and menus (M5-57 to M5-59, M5-62 to M5-64), composition (M5-54).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: `RequestImport` and `RequestExport(id)` raise their events with the id; not attached: nothing raised and entries absent; attach twice throws; `CreateImport`/`CreateExport` call the factories; palette lists `import` and `export-collection`, adds `export-this-collection` for a tab inside a collection with the right name and not for an unfiled request;
  running an entry raises the event; `ExportCollectionCommand` raises with the folder id and is disabled for a nested folder and a request node.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add import and export entries to the shell and the palette`
