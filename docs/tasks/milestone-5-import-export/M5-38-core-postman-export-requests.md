# M5-38: Core: Postman collection export, folders, requests, URLs, headers and bodies

Status: done
Platform: any
Depends on: M5-05, M5-36

## Goal

`PostmanCollectionExporter` writes a collection as a Postman Collection v2.1 JSON file. Auth, variables and scripts are M5-39 and M5-40.

## Design decisions

- Read first: ADR 0016/0017, M5-05 (`DataNodeJson.Obj/Arr/Str/Write`), M5-13 (the inverse of the body mapping), M5-12 (the inverse of the URL mapping), M5-36.
- `src/Yaapic.Core/Export/Postman/PostmanCollectionExporter.cs` (`public sealed class PostmanCollectionExporter : IExporter`, `Id` `postman`, `DisplayName` `Postman collection (v2.1)`, `FileExtension` `.json`, `RequiresCollection` true) and `PostmanRequestWriter.cs` (internal static).
  The result is one `ExportFile(SafeFileName(source.Name) + ".postman_collection.json", utf8 bytes of DataNodeJson.Write(tree, indented: true) + "\n")`; no BOM.
- **Document** (member order exactly): `info` `{ "_postman_id": <collection folder id, "D" format>, "name", "description" (only when not empty), "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json" }`, `item` (array), then `auth`, `event`, `variable` when non-empty (M5-39, M5-40).
- **Items**: siblings in the store order (folders first, then requests, as `CollectionNode.Children` already is). Folder: `{ "name", "description" (when not empty), "item": [...] }` (+ `event` M5-40). Request item: `{ "name", "request": {...}, "response": [] }` (+ `event` M5-40).
- **request** (member order): `method` (upper case), `header`, `body` (omitted when none), `url`, (`auth` M5-39).
  - `header`: array of `{ "key", "value", "type": "text" }` plus `"disabled": true` for disabled entries (member order: key, value, disabled, type).
  - `url`: `{ "raw", "protocol"?, "host": [...], "path": [...], "query": [...] }`. `raw` = `QueryStringSync.Compose(url, <enabled params only>, "")`-style full URL text **with the enabled params only**. Parts: split the stored `Url`: an optional `scheme://` prefix (`protocol`, only when present); the authority = text up to the first `/` (or end) after it,
    minus a trailing `:digits` (`port`, a string, only when present); `host` = the authority split on `.` **outside `{{ }}`**; `path` = the rest after the first `/` split on `/` (empty parts dropped; omitted when empty). `query` (omitted when there are no params): every param `{ "key", "value" }` plus `"disabled": true` when disabled.
    A URL with a `#fragment`: the fragment stays at the end of `raw` and is dropped from the parts. Member order: raw, protocol, host, port, path, query.
  - `body` by kind: `Raw` -> `{ "mode": "raw", "raw": text, "options": { "raw": { "language": "json"|"xml"|"html"|"javascript"|"text" } } }`; `UrlEncoded` -> `{ "mode": "urlencoded", "urlencoded": [ { "key", "value", "disabled"?, "type": "text" } ] }`;
    `FormData` -> `{ "mode": "formdata", "formdata": [ text: { "key", "value", "disabled"?, "type": "text" } | file: { "key", "src": path, "disabled"?, "type": "file" } ] }`; `Binary` -> `{ "mode": "file", "file": { "src": path } }`.
- The report: `Info` `Strings.ExportPostmanDescriptionsLost` is **not** needed; the only entry is `Info` `Strings.ExportFileReference(count)` ("{0} requests reference local files by path; the paths are exported as they are.") when any file field or binary body exists.
  Collection `description` is `FolderDefinition.Description`.
- Strings (EN and FR): `export.fileReference`.

## Out of scope

Auth, scripts, variables, environments (M5-39 to M5-41).

## Acceptance criteria

- [x] Tests with exact expected JSON (indented text compared literally): a collection with a nested folder, a GET with params (one disabled) and headers, a POST with each body kind, URLs `https://api.example.com:8080/v1/users/1?x=1`, `{{baseUrl}}/users/{{id}}`, `http://localhost`, a URL with a fragment, a host with `{{a.b}}`;
  description present and absent; the file-reference `Info`; byte output starts without BOM and ends with `\n`; `Id`, extension, file name.
- [x] A round-trip test using `PostmanCollectionImporter` (M5-12, M5-13): export then import gives equal names, methods, URLs, params, headers and bodies (ids differ). If the importer cards are not `done`, leave this test to M5-66 and say so in the Notes.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export collections as Postman v2.1`
