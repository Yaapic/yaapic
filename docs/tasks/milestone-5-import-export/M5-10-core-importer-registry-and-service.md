# M5-10: Core: importer registry, import service and input reader port

Status: done
Platform: any
Depends on: M5-04, M5-09

## Goal

Detect the format of an input, run its importer to a preview, and apply the preview. The view models call this one service.

## Design decisions

- Read first: ADR 0016 ("Import flow", "Importer contract", limits), M5-04, M5-09.
- `src/Yaapic.Core/Import/ImporterRegistry.cs`: `sealed class ImporterRegistry(IEnumerable<IImporter> importers)`; `IReadOnlyList<IImporter> Importers` (registration order); `IImporter? Find(string id)`;
  `IReadOnlyList<IImporter> Detect(ImportInput input)`: importers whose `Sniff` is not `None`, ordered by confidence (`Certain` first) then registration order; a `Sniff` that throws counts as `None`.
- `src/Yaapic.Core/Import/IImportInputReader.cs` (port, implemented by M5-11): `Task<ImportInput> ReadFileAsync(string path, CancellationToken ct)`; `Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct)`; both throw `ImportException`
  (localized) for a missing path, a file over 50 MiB, a folder over 5000 files or 200 MiB, or no readable file.
- `src/Yaapic.Core/Import/ImportService.cs`: `sealed class ImportService(ImporterRegistry registry, ImportApplier applier)`:
  - `sealed record ImportPreview(IImporter Importer, ImportBundle Bundle)`.
  - `ImportPreview Preview(ImportInput input, string? importerId, ImportOptions options)`: with an id, that importer (unknown id: `ArgumentException`); without, the first of `Detect`, or `ImportException(Strings.ImportFormatNotRecognized)` when none.
    Runs `Import` with `ImportContext.Create(options)`. `ImportException` is rethrown; any other exception except `OperationCanceledException` becomes `ImportException(Strings.ImportFailed(importer.DisplayName, ex.Message))`.
    A bundle with more than 20000 requests throws `ImportException(Strings.ImportTooLarge(count))`. A bundle with zero requests, zero environments and zero globals gets one `Warning` `Strings.ImportNothingFound(importer.DisplayName)` added to its report.
  - `Task<ImportOutcome> ApplyAsync(ImportPreview preview, CancellationToken ct)` calls `applier.ApplyAsync(preview.Bundle, ct)`.
- Strings (EN and FR): `import.formatNotRecognized` = "The format of this file was not recognized. Choose the format manually."; `import.failed` = "The {0} import failed: {1}"; `import.tooLarge` = "This import has {0} requests; the limit is 20000.";
  `import.nothingFound` = "No request, environment or variable was found in this {0} source."

## Out of scope

The concrete importers, file access, UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Import/ImportServiceTests` with fake importers: `Detect` ordering (`Certain` before `Possible`, ties by registration order), a throwing `Sniff` ignored, `Find`; `Preview` with an explicit id and with detection, nothing recognized throws `ImportException`,
  an importer throwing `FormatException` becomes `ImportException` with the importer name, an `ImportException` passes through unchanged, cancellation passes through, more than 20000 requests throws, empty bundle gets the `Warning`; `ApplyAsync` writes through in-memory stores.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the importer registry and the import service`
