# M5-01: ADR 0016: import and export architecture

Status: todo
Platform: any
Depends on: none

## Goal

Write ADR 0016, the reference for every import and export card of milestone 5 (shared model, flow, apply policy, limits, security, UI). Docs only.
The decisions below are final.

## Design decisions (to record in the ADR)

- **Layers.** Importers and exporters are pure code in `Yaapic.Core` (`Yaapic.Core.Import`, `Yaapic.Core.Export`, one sub-folder per format); file access is
  an `Infrastructure` adapter behind ports defined in Core (`IImportInputReader`, `IYamlTreeReader`); view models in `Yaapic.ViewModels`; no business logic in the App projects.
  No new NuGet package: `System.Text.Json` (`JsonDocument`, `Utf8JsonWriter`), `System.IO.Compression`, `System.Text.RegularExpressions` with `[GeneratedRegex]`
  come from the framework; YAML reading uses the VYaml package already referenced (low-level parser, see the spike M5-02). Native AOT: no reflection, no
  `JsonSerializer` on open types, no `dynamic`.
- **Neutral tree.** Foreign JSON and YAML documents are read into `DataNode` (null, bool, number kept as text, string, array, ordered object) in `Yaapic.Core.Import`
  instead of typed DTOs: formats are loosely typed and full of optional shapes, and DTOs would need one source-generated context per format.
- **Import flow.** `IImportInputReader` (file, folder, or pasted text) -> `ImportService.Preview` (detect the format, run the importer, nothing is written) ->
  the user reads the report and confirms -> `ImportService.Apply` (`ImportApplier` writes through the existing stores). Nothing reaches the workspace before the confirmation.
- **Importer contract.** `IImporter { string Id; string DisplayName; ImportConfidence Sniff(ImportInput); ImportBundle Import(ImportInput, ImportContext) }`. `Sniff` is cheap
  (string markers on the first characters, no parse). `Import` is deterministic for a given id generator (`ImportContext.NewId`), never writes, never touches the network or the
  file system, and degrades per item: an item that cannot be converted is skipped and reported, the rest still imports. A source that is not the importer's format at all throws
  `ImportException` (localized message). Format ids: `postman`, `postman-environment`, `curl`, `openapi`, `har`, `insomnia`, `bruno`.
- **Bundle.** `ImportBundle { ImportFolder? Collection; IReadOnlyList<HttpRequestDefinition> UnfiledRequests; IReadOnlyList<EnvironmentDefinition> Environments;
  IReadOnlyList<KeyValueEntry> Globals; ImportReport Report }`. The `Collection` folder (name, description, collection variables, collection scripts) becomes a new top-level
  folder; nested `ImportFolder`s and `ImportRequest`s follow. cURL imports yield `UnfiledRequests`; Postman environment files yield `Environments` and `Globals`.
- **Report.** Every conversion records entries `ImportEntry(Severity, Path, Message)`, severity `Info` (converted, note), `Warning` (converted with a loss or a risk) or `Skipped`
  (not imported). Messages are localized strings built in Core (`Strings`, keys `import.<format>.<name>`, EN and FR). `Path` is the location in the source
  (`Folder/Sub/Request`, or `paths./pets.get`, or `entries[3]`). Losses that hit many items (descriptions, saved response examples, ordering) are reported once per import, with a count.
- **Apply policy** (`ImportApplier`): ids are always new (the importer generates them); the collection becomes a new top-level folder, renamed `Name (2)`, `Name (3)` on a clash
  with an existing top-level folder (ignoring case) and reported as `Info`; environments clash the same way; globals add only the keys not yet present (existing values win, count reported);
  parents are saved before children; one failing save is reported `Skipped` and the import continues (no rollback; deleting the new top-level folder undoes the requests).
  **Secrets:** rows with `IsSecret` are stored through the existing secret-aware stores only when the vault is unlocked; otherwise their value is dropped, `IsSecret` cleared and one
  `Warning` says how many values must be entered again. Secret rows of nested folders are never stored with a value (nested folders cannot hold secrets).
- **Unsupported constructs.** Methods outside GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS skip the request. Authentication kinds Yaapic lacks import the request without auth and warn.
  Authentication inherited from a parent is **copied onto each request that has none** (nearest ancestor wins), reported once as `Info`; this keeps the card M4-28 (blocked) out of the way.
  Dynamic variables: `$guid`, `$timestamp`, `$isoTimestamp`, `$randomInt` exist; `$randomUUID` maps to `$guid`; any other `{{$name}}` is kept as written and warned once per name.
- **Scripts.** Postman and Bruno scripts go through `ScriptConverter` (U-05), as JavaScript; each finding becomes a `Warning` with the script member and line. One aggregated `Warning`
  tells the user that the collection contains scripts to review before sending. Scripts of other tools (Insomnia) are not converted and are reported.
- **Security of untrusted files.** Never fetch a URL, never read a file named by the document (`$ref` to a file or URL is not followed and is reported). Limits enforced by the reader and the
  service: 50 MiB per file, 5000 files and 200 MiB per folder import, JSON depth 64, 20000 requests per import (`Strings.ImportTooLarge`). Every imported **file reference** (multipart
  file, binary body, `@file`) gets a `Warning` naming the path, because the file is read and sent when the request is sent. Text is decoded as UTF-8 (BOM stripped).
- **Export contract.** `IExporter { string Id; string DisplayName; string FileExtension; bool RequiresCollection; ExportResult Export(ExportSource) }`; `ExportSource` holds one top-level
  collection (`CollectionNode`), the environments to export and optionally the globals; `ExportResult` holds `ExportFile(Path, byte[])` entries and an `ImportReport` (same type, reused).
  Output is deterministic: UTF-8 without BOM, `\n`, stable key order, no timestamps. One file is saved as is; several files are packaged as a `.zip` (`ExportPackager`).
  **Secret values are never exported**: `ExportSourceReader` blanks the value of every secret row (the vault is not read for export) and the report says how many.
  Export scripts: Postman gets a `yaapic.` to `pm.` rewrite; Bruno and Insomnia get the script text unchanged and a `Warning`.
- **cURL export is code generation** (M4-51, `CurlCodeGenerator`): no export card; cURL import is M5-17 and M5-18.
- **UI.** Palette entries and a sidebar "Import..." item open the import dialog (choose a file, a folder (Bruno) or paste text; the detected format can be overridden; preview with counts and the
  filterable report; Import button). Export is offered on a top-level folder (context menu) and in the palette; it asks for the format, then a save dialog (`IDialogService.SaveFileAsync`).
  `IDialogService` gains `PickFolderAsync`.
- **Out of scope for the milestone**: Insomnia 5 YAML, HAR export, pasting cURL into the URL box (blocked cards), exporting history, importing from a URL, drag and drop of files, undo of an import,
  Postman cloud API, Swagger/OpenAPI editing, gRPC/GraphQL/WebSocket items (milestone 7).

## Out of scope

Code, UI.

## Acceptance criteria

- [ ] `docs/adr/0016-import-export-architecture.md` contains every decision above (status Accepted, options considered: typed DTOs per format versus `DataNode`, one importer per file versus
  per format, rollback versus best effort apply); the ADR index in `docs/adr/README.md` lists it.
- [ ] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0016 import and export architecture`
