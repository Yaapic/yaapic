# M5-04: Core: import model, report and importer contract

Status: done
Platform: any
Depends on: M5-01

## Goal

The shared types every importer produces and every consumer reads. No importer yet.

## Design decisions

- Read first: ADR 0016, `src/Yaapic.Domain/Collections/FolderDefinition.cs`, `src/Yaapic.Domain/Http/HttpRequestDefinition.cs`, `EnvironmentDefinition.cs`, `KeyValueEntry.cs`, `src/Yaapic.Core/Secrets/SecretRows.cs`
  (how `Strings` are used from Core), `tests/Yaapic.TestDoubles` (where test helpers live).
- All in `src/Yaapic.Core/Import/` (namespace `Yaapic.Core.Import`), one type per file, every public member documented:
  - `enum ImportSeverity { Info, Warning, Skipped }`; `sealed record ImportEntry(ImportSeverity Severity, string Path, string Message)`.
  - `sealed class ImportReport`: `IReadOnlyList<ImportEntry> Entries`; `void Add(ImportSeverity, string path, string message)`; `Info(path, message)`, `Warning(path, message)`, `Skipped(path, message)`;
    `bool AddOnce(ImportSeverity, string path, string message)` (does nothing and returns false when an entry with the same severity **and message** exists; used for "once per distinct name" warnings);
    `int Count(ImportSeverity)`; `bool HasProblems` (any `Warning` or `Skipped`); `void AddRange(IEnumerable<ImportEntry>)`.
  - `static class ImportPath` with `string Join(params ReadOnlySpan<string> parts)`: parts joined with `/`, empty parts dropped; returns `""` when nothing is left.
  - `sealed record ImportFile(string Path, string Text)` (`Path` relative to the chosen root with `/` separators; for a single file it is the file name) and
    `sealed record ImportInput(string Name, IReadOnlyList<ImportFile> Files)` with `Single` (the first file, `InvalidOperationException` when empty), and `static ImportInput FromText(string name, string text)`
    (one file named `name`). `Name` is the file or folder name without extension (the HAR and cURL collection names use it).
  - `sealed record ImportOptions(bool IncludeStaticResources = false)` (read by M5-26) with `static ImportOptions Default`.
  - `sealed class ImportContext(Func<Guid> newId, ImportOptions options)`: `Guid NewId()`, `ImportOptions Options`, `ImportReport Report` (created in the constructor).
    `static ImportContext Create(ImportOptions? options = null)` uses `Guid.NewGuid`.
  - `abstract record ImportItem`; `sealed record ImportFolder(FolderDefinition Folder, IReadOnlyList<ImportItem> Children) : ImportItem`; `sealed record ImportRequest(HttpRequestDefinition Request) : ImportItem`.
  - `sealed record ImportBundle(ImportFolder? Collection, IReadOnlyList<HttpRequestDefinition> UnfiledRequests, IReadOnlyList<EnvironmentDefinition> Environments, IReadOnlyList<KeyValueEntry> Globals, ImportReport Report)`
    with `int RequestCount` (all requests in the tree plus unfiled), `int FolderCount` (all folders in the tree, the collection folder included) and `static ImportBundle Empty(ImportReport report)`.
  - `enum ImportConfidence { None, Possible, Certain }`; `interface IImporter { string Id { get; } string DisplayName { get; } ImportConfidence Sniff(ImportInput input); ImportBundle Import(ImportInput input, ImportContext context); }`
    (document the contract of ADR 0016 on the interface); `sealed class ImportException(string message) : Exception(message)` (plus the standard constructors that CA1032 asks for if enabled).
- `tests/Yaapic.TestDoubles/SequentialIds.cs`: `public static Func<Guid> Create(int start = 1)` returns ids `00000000-0000-0000-0000-000000000001`, `...02`, ... (format the counter as the last 12 hex digits).
  Importer tests build `new ImportContext(SequentialIds.Create(), ImportOptions.Default)`.
- Strings (EN and FR, keys in `strings.en.json` and `strings.fr.json`): none yet.

## Out of scope

Importers, the applier (M5-09), the registry (M5-10).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Import`: `ImportReport` counts, `HasProblems`, `AddOnce` (same message twice adds one, same message with another severity adds two), `ImportPath.Join` (empty parts, no parts),
  `ImportBundle.RequestCount` and `FolderCount` on a nested tree plus unfiled requests, `ImportContext.NewId` order with `SequentialIds`, `ImportInput.FromText`, `Single` on empty files throws.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the import model, report and importer contract`
