# M5-36: Core: export model, contract, registry and source reader

Status: done
Platform: any
Depends on: M5-04

## Goal

The shared export types and the service that reads a collection (and environments) from the stores, blanking secrets.

## Design decisions

- Read first: ADR 0016 ("Export contract"), `src/Yaapic.Domain/Collections/CollectionNode.cs`, `src/Yaapic.Core/Storage/ICollectionStore.cs`, `IEnvironmentStore.cs`, `IGlobalsStore.cs`, `docs/file-format.md` (Secret variables), M5-04 (`ImportReport` is reused as the export report).
- `src/Yaapic.Core/Export/` (namespace `Yaapic.Core.Export`), one type per file, XML docs:
  - `sealed record ExportFile(string Path, byte[] Content)` (`Path`: a relative path with `/`, also the suggested file name for a single file).
  - `sealed record ExportSource(CollectionNode? Collection, IReadOnlyList<EnvironmentDefinition> Environments, IReadOnlyList<KeyValueEntry> Globals, string Name)`; `Name` is the collection name, or `Environments` when there is no collection.
  - `sealed record ExportResult(IReadOnlyList<ExportFile> Files, ImportReport Report)`.
  - `interface IExporter { string Id { get; } string DisplayName { get; } string FileExtension { get; } bool RequiresCollection { get; } ExportResult Export(ExportSource source); }` (document: pure, deterministic, never throws for content: unconvertible items are skipped and reported; `ExportException` only for an impossible source, e.g. no collection when one is required).
  - `sealed class ExportException(string message) : Exception(message)`.
  - `sealed class ExporterRegistry(IEnumerable<IExporter> exporters)` with `Exporters` (registration order) and `IExporter? Find(string id)`.
  - `ExportSourceReader(ICollectionStore collections, IEnvironmentStore environments, IGlobalsStore globals)`: `Task<ExportSource> ReadAsync(Guid? collectionId, IReadOnlyList<Guid> environmentIds, bool includeGlobals, CancellationToken ct)`.
- **Reader rules**: `collectionId` set: `LoadTreeAsync`, find the **top-level** folder node with that id (not found or not a folder: `ExportException(Strings.ExportCollectionNotFound)`); null: `Collection` null. `environmentIds` selects environments by id from `ListAsync` (unknown ids ignored; **empty list means none**); `includeGlobals` loads the globals, otherwise empty.
  **Secrets** (policy of ADR 0016): every `KeyValueEntry` with `IsSecret` in the collection folder variables, in every environment and in the globals gets `Value = ""` (keeping `IsSecret = true`) because the secret-aware stores fill secret values from the vault when it is unlocked; the node tree is rebuilt with the blanked folder
  (`CollectionNode` is immutable: write a small recursive rebuild helper using the node factories; read the file to see them). The blanked count is reported through a report the caller passes in: the signature is `ReadAsync(Guid? collectionId, IReadOnlyList<Guid> environmentIds, bool includeGlobals, ImportReport report, CancellationToken ct)` and it adds `Strings.ExportSecretsOmitted(count)` as an `Info` when the count is greater than 0 (the view model merges this report with the exporter's).
- Strings (EN and FR): `export.collectionNotFound` = "The collection to export no longer exists."; `export.secretsOmitted` = "{0} secret values were not exported (secret values never leave the vault)."

## Out of scope

Exporters (M5-38 onward), packaging (M5-37), UI.

## Acceptance criteria

- [x] Tests with in-memory stores: the collection is found by id; a nested folder id is refused; secret rows blanked in collection variables, in environments and in globals while non-secret rows keep their values (use stores pre-filled with values and flags); the report `Info` count; environment selection by ids and the empty list;
  `includeGlobals` false gives no globals; registry find and order; a collection with a nested tree is rebuilt with the same shape.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the export model, registry and source reader`
