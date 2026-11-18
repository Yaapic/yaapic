# M5-12: Core: Postman collection import, folders, requests, URLs and headers

Status: done
Platform: any
Depends on: M5-05, M5-06

## Goal

`PostmanCollectionImporter` reads a Postman collection (v2.0 and v2.1) into folders and requests with URL, query parameters and headers. Bodies, auth, scripts and variables are added by M5-13 to M5-15.

## Design decisions

- Read first: ADR 0016, ADR 0017, M5-04, M5-05, M5-06, `docs/file-format.md` (request model), `src/Yaapic.Domain/Http/HttpRequestDefinition.cs`.
- Files: `src/Yaapic.Core/Import/Postman/PostmanCollectionImporter.cs` (`public sealed class PostmanCollectionImporter : IImporter`, `Id` `postman`, `DisplayName` `Postman collection`) and `PostmanItems.cs` (internal static, the item walk and the request mapping, extended by the next cards).
  Input is `input.Single.Text` parsed with `DataNodeJson.Parse` (JSON only; a `FormatException` becomes `ImportException(Strings.ImportInvalidJson(...))`).
- **Sniff**: `Certain` when the first 4096 characters contain `getpostman.com/json/collection`; `Possible` when they contain both `"_postman_id"` and `"item"`; else `None`.
- **Root**: if the document has a `collection` object member and no `info`, use that object (API export wrapper). Missing `info` or `item` array: `ImportException(Strings.ImportPostmanNotACollection)`.
  Collection folder: `FolderDefinition(id = context.NewId(), Name = SafeName(info.name, input.Name), Description = info.description (a string; an object with `content` uses that), Variables = [], Scripts = empty)`; variables and scripts come from M5-15.
- **Items**: an item with an `item` array is a folder (`FolderDefinition` with `name`, `description`); otherwise, when it has `request`, a request. Anything else: `Skipped` `Strings.ImportPostmanItemUnknown(path)`. Depth limit 32 folders (deeper: `Skipped` the subtree).
  Order is the source order (the store sorts anyway); report once `Info` `Strings.ImportOrderLost` ("Yaapic sorts folders and requests by name; the order of the source is not kept.") when the collection has more than one item at some level.
- **Request mapping** (the `request` value is a URL string, or an object):
  - `name = SafeName(item.name, DescribeRequest(method, url))`.
  - `method`: `request.method` through `TryParseMethod`, default `GET` when absent (string form: GET); unsupported: `Skipped` `Strings.ImportMethodUnsupported`, request not imported.
  - **URL**: when `request.url` is a string it is the raw URL. When it is an object, the raw URL is `url.raw` when non-empty, otherwise composed: `protocol + "://"` (when present) + `host` joined by `.` (host may be a string) + `":" + port` (when present) + `"/" + path` joined by `/` (path elements may be
    strings or `{value}` objects; drop empty ones). Then `SplitUrl(raw)` gives `Url` and the query params from the raw text. When `url.query` is an array it **replaces** the params from the raw text: each element `{key, value, disabled}` becomes `KeyValueEntry(key ?? "", value ?? "", Enabled = !disabled)`
    (null value becomes `""`; elements without a key and a value are dropped). Path variables: `url.variable` array of `{key, value}` builds the dictionary for `SubstitutePathVariables`. Finally `TranslateDynamicVariables` is applied to the URL and to every param value.
  - **Headers**: `request.header` array of `{key, value, disabled}` -> `KeyValueEntry(key, TranslateDynamicVariables(value), Enabled = !disabled)`; keep every header (no managed-header filtering); entries without a key are dropped. `request.header` as a single string (`"A: b\nC: d"`) is split on lines at the first `:`.
  - Everything else of the request (`description`, `response` examples) is dropped; count them: one `Info` per import `Strings.ImportPostmanDroppedExamples(count)` when any saved response exists, and `Strings.ImportDescriptionsDropped(count)` when any request description exists
    (strings shared by all formats: `import.descriptionsDropped` = "Descriptions of {0} requests were not imported.").
  - `Settings = RequestSettings.Default`, `Auth = RequestAuth.None`, `Body = RequestBody.None`, `Scripts = ScriptSet.Empty` until the next cards.
- Strings (EN and FR): `import.invalidJson` already exists (M5-05); `import.postman.notACollection` = "This file is not a Postman collection (no \"info\" or \"item\")."; `import.postman.itemUnknown` = "\"{0}\" is neither a folder nor a request and was skipped.";
  `import.orderLost`; `import.postman.droppedExamples` = "Saved response examples ({0}) were not imported."; `import.descriptionsDropped`.

## Out of scope

Bodies (M5-13), auth (M5-14), scripts, variables (M5-15), environments (M5-16).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Import/Postman/PostmanCollectionImporterTests` with inline JSON (raw string literals) and `SequentialIds`: sniff cases (certain, possible, none, a non-JSON text); the wrapper `{"collection": ...}`; a collection with two folder levels and requests (names, parents, ids in walk order);
  string `request` form; URL as string, as object with `raw`, as object composed from `protocol/host/port/path` (path elements as strings and as `{value}`), with `query` that overrides the raw query and a disabled param, with `variable` (`:id` substituted, `:name` without value becomes `{{name}}`);
  headers with a disabled one and a string form; `TRACE` skipped with the entry; an unknown item skipped; `$randomUUID` rewritten and `$randomFirstName` warned once; the order `Info` appears once; the dropped examples and descriptions entries; a missing `info` throws `ImportException`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Postman collections, folders, requests, URLs and headers`
