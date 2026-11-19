# M5-27: Core: Insomnia v4 import, workspace, folders, requests and bodies

Status: done
Platform: any
Depends on: M5-05, M5-06

## Goal

`InsomniaImporter` reads an Insomnia v4 JSON export into a collection with folders and requests (URL, parameters, headers, body). Authentication, template tags and environments are M5-28 and M5-29.

## Design decisions

- Read first: ADR 0017 (Insomnia), M5-05, M5-06, M5-13 (body shapes). The field names below are the documented Insomnia export fields; fixtures are written by hand from this card (state in the Notes whether a real export was available to compare).
- `src/Yaapic.Core/Import/Insomnia/InsomniaImporter.cs` (`public sealed class InsomniaImporter : IImporter`, `Id` `insomnia`, `DisplayName` `Insomnia`) and `InsomniaRequests.cs` (internal static). **Sniff**: `Certain` when the first 8192 characters contain `"__export_format"`; `Possible` when they contain `"_type"` and `"resources"`; else `None`.
- **Document**: `{"_type": "export", "__export_format": 3|4, "resources": [...]}`; other `__export_format` values: `ImportException(Strings.ImportInsomniaVersion(value))`; a missing `resources` array: `ImportException(Strings.ImportInsomniaNotAnExport)`.
  Each resource has `_id`, `_type`, `parentId`, `name`. Index the resources by `_id`; children of a parent keep resource array order.
- **Workspace**: the first resource with `_type` `workspace` is the collection (`name`, `description`); the collection folder is `FolderDefinition(NewId, SafeName(name, input.Name), description, [], empty)`. No workspace: the collection is named after `input.Name`; resources with unknown parents attach to the collection. Only `scope` `collection` and `design` workspaces are read; others are read too (no check).
  More than one workspace: only the first is imported, the others `Skipped` with `Strings.ImportInsomniaOtherWorkspaces(count)`.
- **Folders**: `_type` `request_group` (children via `parentId`; `name`, `description`) -> nested folders (depth limit 32). **Requests**: `_type` `request`. Other types (`grpc_request`, `websocket_request`, `unit_test`, `api_spec`, `cookie_jar`, `mock_*`, `environment` (M5-29)): `grpc_request` and `websocket_request` get one aggregated `Skipped`
  `Strings.ImportInsomniaUnsupportedTypes(count)` ("{0} gRPC and WebSocket requests are not supported yet."); the rest are ignored silently except one `Info` `Strings.ImportInsomniaIgnoredTypes(count)` for `cookie_jar`, `api_spec`, `unit_test*`.
- **Request mapping**:
  - `name = SafeName(name, DescribeRequest(method, url))`; `method` through `TryParseMethod` (unsupported: `Skipped`).
  - `url`: `SplitUrl(url)`, then `parameters` array of `{name, value, disabled}` are added **after** the params already in the URL: `KeyValueEntry(name, value, Enabled = !disabled)`. (Insomnia keeps query pairs in `parameters` and the URL without query; both shapes are handled.)
  - `headers`: `{name, value, disabled}` -> entries (`Enabled = !disabled`), all kept.
  - `body` (absent or empty object: none) by `mimeType` (lower case without parameters): `application/json`, `application/xml`, `text/xml`, `text/html`, `text/plain`, `application/javascript` -> `Raw` with `ContentTypeFromMime` and `text` (a `text` that is absent: none);
    `application/graphql` -> `Raw Json` with `{"query": <text query>, "variables": ...}`: Insomnia stores a JSON string `{"query":..,"variables":..}` in `text` already -> kept as is, `Raw Json`;
    `application/x-www-form-urlencoded` -> `UrlEncoded` from `params` `{name, value, disabled}`; `multipart/form-data` -> `FormData` from `params` (`type` `file` -> `File` field with `Value = fileName`, plus `Warning` `Strings.ImportFileReference`; others `Text`); `application/octet-stream` with `fileName` -> `Binary` + `Warning` `Strings.ImportFileReference`;
    no `mimeType` but `text` -> `Raw Text`; other `mimeType` with `text` -> `Raw Text` and an explicit header `Content-Type: <mimeType>` unless a `Content-Type` header exists.
  - `settingFollowRedirects`: `off` -> `FollowRedirects = false`, `on` or `global` -> true. Other `setting*` fields are ignored. `description` dropped (counted for `Strings.ImportDescriptionsDropped`).
  - `Auth` none, scripts: if `preRequestScript` or `afterResponseScript` is non-empty (newer exports) one `Warning` per import `Strings.ImportInsomniaScripts(count)` ("{0} Insomnia scripts were not imported: they use the insomnia API, which Yaapic does not provide.").
- Variables: `{{ _.name }}` conversion and template tags are **not** applied here (M5-28 does it for every string field).
- Strings (EN and FR): `import.insomnia.version` = "Insomnia export format {0} is not supported (3 and 4 are)."; `import.insomnia.notAnExport` = "This file is not an Insomnia export (no \"resources\")."; `import.insomnia.otherWorkspaces` = "{0} other workspaces were not imported; export them separately.";
  `import.insomnia.unsupportedTypes`; `import.insomnia.ignoredTypes` = "{0} cookie jars, API specs and test suites were ignored."; `import.insomnia.scripts`.

## Out of scope

Auth and template tags (M5-28), environments (M5-29), Insomnia 5 (blocked M5-30).

## Acceptance criteria

- [x] Tests with inline JSON: sniff; workspace and two folder levels; request with `parameters` on a URL that also has a query; disabled header and param; each body mimeType; multipart with file; octet-stream; graphql; unknown mime with header added; redirect settings; unsupported method; grpc request aggregated entry; unknown parent attaches to the collection;
  a version 2 export throws; two workspaces; scripts warning; ignored types `Info`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Insomnia folders, requests and bodies`

## Notes

No real Insomnia export was available: fixtures are written by hand from the card.
