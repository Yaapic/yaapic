# ADR 0016: Import and export architecture

Status: Accepted

See also: [ADR 0005](0005-storage-format.md), [ADR 0008](0008-collections-globals-history-storage.md),
[ADR 0009](0009-scripting.md), [ADR 0010](0010-secret-vault.md), [ADR 0015](0015-response-diff-and-code-generation.md).
Every import and export card of milestone 5 reads this ADR first.

## Context

Milestone 5 imports collections, environments and requests from other tools (Postman, cURL, OpenAPI/Swagger 2.0, HAR,
Insomnia, Bruno) and exports to Postman, Bruno and Insomnia. Source files are untrusted, loosely typed and full of
optional shapes. The architecture, the apply policy and the limits are fixed here.

## Options considered

- **Typed DTOs per format versus a neutral `DataNode` tree.** DTOs give typed access but every format has many optional
  shapes, and Native AOT needs one source-generated JSON context per format. A neutral tree (`DataNode`) read once from
  JSON or YAML keeps the importers small and tolerant. Chosen: `DataNode`.
- **One importer per file kind versus per format.** Per file kind multiplies importers (Postman collection and
  environment, OpenAPI JSON and YAML...). Per format keeps detection and mapping together. Chosen: one importer per
  format id; Postman environments are their own id because their shape is unrelated to a collection.
- **Rollback versus best-effort apply.** Stores have no transaction; a rollback would need a delete pass that can itself
  fail. Best effort with a report, plus the fact that deleting the new top-level folder undoes the requests, is simpler
  and honest. Chosen: best effort.

## Decision

### Layers

- Importers and exporters are pure code in `Yaapic.Core` (`Yaapic.Core.Import`, `Yaapic.Core.Export`, one sub-folder per
  format). File access is an `Infrastructure` adapter behind ports defined in Core (`IImportInputReader`,
  `IYamlTreeReader`). View models live in `Yaapic.ViewModels`; the App projects hold no business logic.
- No new NuGet package: `System.Text.Json` (`JsonDocument`, `Utf8JsonWriter`), `System.IO.Compression` and
  `System.Text.RegularExpressions` with `[GeneratedRegex]` come from the framework. YAML reading uses the VYaml package
  already referenced (low-level parser, see the spike M5-02).
- Native AOT: no reflection, no `JsonSerializer` on open types, no `dynamic`.

### Neutral tree

Foreign JSON and YAML documents are read into `DataNode` (null, bool, number kept as text, string, array, ordered
object) in `Yaapic.Core.Import`.

### Import flow

`IImportInputReader` (file, folder or pasted text) -> `ImportService.Preview` (detect the format, run the importer,
nothing is written) -> the user reads the report and confirms -> `ImportService.Apply` (`ImportApplier` writes through
the existing stores). Nothing reaches the workspace before the confirmation.

### Importer contract

`IImporter { string Id; string DisplayName; ImportConfidence Sniff(ImportInput); ImportBundle Import(ImportInput, ImportContext) }`.

- `Sniff` is cheap: string markers on the first characters, no parse.
- `Import` is deterministic for a given id generator (`ImportContext.NewId`), never writes, never touches the network or
  the file system, and degrades per item: an item that cannot be converted is skipped and reported, the rest still
  imports. A source that is not the importer's format at all throws `ImportException` (localized message).
- Format ids: `postman`, `postman-environment`, `curl`, `openapi`, `har`, `insomnia`, `bruno`.

### Bundle

`ImportBundle { ImportFolder? Collection; IReadOnlyList<HttpRequestDefinition> UnfiledRequests;
IReadOnlyList<EnvironmentDefinition> Environments; IReadOnlyList<KeyValueEntry> Globals; ImportReport Report }`.
The `Collection` folder (name, description, collection variables, collection scripts) becomes a new top-level folder;
nested `ImportFolder`s and `ImportRequest`s follow. cURL imports yield `UnfiledRequests`; Postman environment files
yield `Environments` and `Globals`.

### Report

Every conversion records `ImportEntry(Severity, Path, Message)`, severity `Info` (converted, note), `Warning`
(converted with a loss or a risk) or `Skipped` (not imported). Messages are localized strings built in Core
(`Strings`, keys `import.<format>.<name>`, EN and FR). `Path` is the location in the source (`Folder/Sub/Request`,
`paths./pets.get`, `entries[3]`). Losses that hit many items (descriptions, saved response examples, ordering) are
reported once per import, with a count.

### Apply policy (`ImportApplier`)

- Ids are always new (the importer generates them).
- The collection becomes a new top-level folder, renamed `Name (2)`, `Name (3)` on a clash with an existing top-level
  folder (ignoring case) and reported as `Info`; environments clash the same way.
- Globals add only the keys not yet present (existing values win, count reported).
- Parents are saved before children. One failing save is reported `Skipped` and the import continues: no rollback;
  deleting the new top-level folder undoes the requests.
- **Secrets:** rows with `IsSecret` are stored through the existing secret-aware stores only when the vault is
  unlocked; otherwise their value is dropped, `IsSecret` cleared and one `Warning` says how many values must be entered
  again. Secret rows of nested folders are never stored with a value (nested folders cannot hold secrets).

### Unsupported constructs

- Methods outside GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS skip the request.
- Authentication kinds Yaapic lacks import the request without auth and warn.
- Authentication inherited from a parent is **copied onto each request that has none** (nearest ancestor wins),
  reported once as `Info`; this keeps the card M4-28 (blocked) out of the way.
- Dynamic variables: `$guid`, `$timestamp`, `$isoTimestamp`, `$randomInt` exist; `$randomUUID` maps to `$guid`; any
  other `{{$name}}` is kept as written and warned once per name.

### Scripts

Postman and Bruno scripts go through `ScriptConverter` (U-05), as JavaScript; each finding becomes a `Warning` with the
script member and line. One aggregated `Warning` tells the user the collection contains scripts to review before
sending. Scripts of other tools (Insomnia) are not converted and are reported.

### Security of untrusted files

- Never fetch a URL, never read a file named by the document (`$ref` to a file or URL is not followed and is reported).
- Limits enforced by the reader and the service: 50 MiB per file, 5000 files and 200 MiB per folder import, JSON depth
  64, 20000 requests per import (`Strings.ImportTooLarge`).
- Every imported **file reference** (multipart file, binary body, `@file`) gets a `Warning` naming the path, because the
  file is read and sent when the request is sent.
- Text is decoded as UTF-8 (BOM stripped).

### Export contract

`IExporter { string Id; string DisplayName; string FileExtension; bool RequiresCollection; ExportResult Export(ExportSource) }`.

- `ExportSource` holds one top-level collection (`CollectionNode`), the environments to export and optionally the
  globals. `ExportResult` holds `ExportFile(Path, byte[])` entries and an `ImportReport` (same type, reused).
- Output is deterministic: UTF-8 without BOM, `\n`, stable key order, no timestamps.
- One file is saved as is; several files are packaged as a `.zip` (`ExportPackager`).
- **Secret values are never exported**: `ExportSourceReader` blanks the value of every secret row (the vault is not read
  for export) and the report says how many.
- Scripts: Postman gets a `yaapic.` to `pm.` rewrite; Bruno and Insomnia get the script text unchanged and a `Warning`.

### cURL export

cURL export is code generation (M4-51, `CurlCodeGenerator`): there is no export card. cURL import is M5-17 and M5-18.

### UI

Palette entries and a sidebar "Import..." item open the import dialog (choose a file, a folder (Bruno) or paste text;
the detected format can be overridden; preview with counts and the filterable report; Import button). Export is offered
on a top-level folder (context menu) and in the palette; it asks for the format, then a save dialog
(`IDialogService.SaveFileAsync`). `IDialogService` gains `PickFolderAsync`.

### Out of scope for the milestone

Insomnia 5 YAML, HAR export, pasting cURL into the URL box (blocked cards), exporting history, importing from a URL,
drag and drop of files, undo of an import, Postman cloud API, Swagger/OpenAPI editing, gRPC/GraphQL/WebSocket items
(milestone 7).

## Native AOT

No reflection anywhere: `DataNode` is built by hand from `JsonDocument` and the VYaml parser; writers use
`Utf8JsonWriter`; regexes use `[GeneratedRegex]`. The YAML reader is validated under AOT by the spike M5-02.
