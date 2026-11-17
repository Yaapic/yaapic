# M5-33: Core: Bruno import, collection structure and auth inheritance

Status: todo
Platform: any
Depends on: M5-32

## Goal

`BrunoImporter` walks a Bruno collection folder (`bruno.json`, `folder.bru`, `*.bru`) into the collection folder tree, resolving `auth: inherit`.

## Design decisions

- Read first: ADR 0016/0017, M5-04 (`ImportInput` with relative `/` paths), M5-31, M5-32.
- `src/Yaapic.Core/Import/Bruno/BrunoImporter.cs` (`public sealed class BrunoImporter : IImporter`, `Id` `bruno`, `DisplayName` `Bruno collection`) and `BrunoCollection.cs` (internal static). **Sniff**: `Certain` when a file path equals `bruno.json` (or ends `/bruno.json`) or there are at least two `.bru` files; `Possible` when there is one `.bru` file; else `None`.
- **Layout** (paths relative to the picked folder; the collection root is the directory that holds `bruno.json`; when there is none, the root is the picked folder): `bruno.json` -> `{"name": "...", "version": "1", "type": "collection", "ignore": [...]}` read with `DataNodeJson.Parse` (invalid JSON: `Warning`, name falls back to `input.Name`);
  `collection.bru` at the root holds collection-level blocks; `<dir>/folder.bru` holds folder blocks (`meta { name, seq }`, `auth`, `headers`, scripts, vars); `environments/*.bru` are environments (M5-35, skipped by the walk here); every other `*.bru` is a request. Directories listed in `bruno.json` `ignore`, `node_modules` and `.git` are skipped.
  Files that are not `.bru` or `bruno.json` are ignored.
- **Folders**: every directory that holds at least one request or sub-folder becomes a nested `FolderDefinition(NewId, name, "", [], empty)`; `name` = `meta.name` of its `folder.bru` when present, else the directory name. Depth limit 32. Folder-level `headers` blocks of `collection.bru` and `folder.bru`: Bruno prepends those headers to every request below.
  Yaapic has no header inheritance, so they are **copied into each request** (collection headers first, then folder headers outermost to innermost, then the request's own; a request header with the same name ignoring case replaces the inherited one) and reported once `Info` `Strings.ImportBrunoHeadersInherited(count)`.
- **Collection folder**: `FolderDefinition(NewId, SafeName(bruno.json name, input.Name), "", [], empty)`; variables and scripts are filled by M5-34.
- **Auth inheritance**: request `auth: inherit` takes the nearest ancestor's `auth` (the `auth` mode + `auth:<mode>` block of `folder.bru` files, then `collection.bru`), mapped with the same table as M5-32 (extract the shared mapping from `BrunoRequests` into `BrunoAuth.Map(BruFile, string path, ImportReport)` in this card); no ancestor auth -> `None`.
  Count the copies: one `Info` per import `Strings.ImportAuthInherited(count)` (shared with M5-14). A folder or the collection with `auth: inherit` or none is transparent.
- **Ordering**: source `seq` is not kept; one `Info` `Strings.ImportOrderLost` per import when any `seq` exists (shared string, M5-12).
- A request file that fails to parse into a request (no verb, unsupported verb) is skipped by `BrunoRequests` with its entry; the walk continues. Path in entries = `ImportPath.Join` of the directory names and the request name.
- **Dispatch**: a `bruno.json` or `.bru` files -> this importer. A single `.bru` file picked alone (one file, no `bruno.json`) imports as one unfiled request through the same mapping (collection = none).
- Strings (EN and FR): `import.bruno.headersInherited` = "{0} requests got the headers of their folder or collection copied onto them."; `import.orderLost` and `import.authInherited` (shared).

## Out of scope

Scripts, variables (M5-34), environments (M5-35), OpenCollection YAML.

## Acceptance criteria

- [ ] Tests with in-memory `ImportInput` files: a collection with `bruno.json`, two nested folders with `folder.bru` names, requests; folder name fallback to the directory name; a directory with no requests omitted; `ignore` honoured; header inheritance from collection and folder with a replaced header and the `Info` count;
  auth inheritance (folder over collection, request override, transparent folder) with exact `RequestAuth`; unparsable request skipped and the rest imported; a single `.bru` file imports as one unfiled request; no `bruno.json` uses the input name; sniff cases; `seq` order `Info`.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import the structure of a Bruno collection`
