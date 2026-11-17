# M5-42: Core: OpenAPI export, info, servers, paths, operations and parameters

Status: todo
Platform: any
Depends on: M5-05, M5-36

## Goal

`OpenApiDocumentBuilder` infers an OpenAPI 3.0.3 document (as a `DataNode`) from the requests of a collection. Bodies and security are M5-43, which also adds the JSON and YAML exporters.

## Design decisions

- Read first: ADR 0016/0017 ("OpenAPI export is a best-effort description inferred from requests"), M5-05, M5-36, M5-20 (the import side, for symmetry), `src/Yaapic.Core/Http/QueryStringSync.cs`.
- `src/Yaapic.Core/Export/OpenApi/OpenApiDocumentBuilder.cs` (internal static): `DataNode Build(ExportSource source, ImportReport report)`; the document member order is: `openapi` (`"3.0.3"`), `info`, `servers`, `tags`, `paths`, `components` (M5-43 adds `components`).
- **info**: `{ "title": collection name, "version": "1.0.0", "description": collection description (omitted when empty) }`. One `Info` `Strings.ExportOpenApiInferred` per export: "This OpenAPI document was inferred from the requests: responses and schemas are not known and were left generic.".
- **Operations**: walk the collection tree depth first (store order). For each request, split its stored `Url`:
  - `origin`: `scheme://authority` when the URL has a scheme, otherwise the leading `{{variable}}` token when the URL starts with `{{...}}` (followed by `/` or the end), otherwise `""`.
  - `path`: the rest up to `?` or `#`, defaulting to `/`; every `{{name}}` in it becomes `{name}` and every `:name` segment becomes `{name}`; a path not starting with `/` gets one.
  - **servers**: distinct origins in first-seen order. An origin that is a `{{variable}}` token is replaced by the **value of that variable among the collection variables** when it is non-empty (trailing `/` removed), else the server is `{ "url": "/" }` with a `Warning` `Strings.ExportOpenApiUnresolvedServer(name)`.
    Requests with an empty origin use no server of their own. No origin anywhere: `servers` omitted. Each server is `{ "url": ... }`.
  - **paths** in first-seen order; under each path the operations by lower-case method in the order the requests were found. A second request with the same method and path is **skipped** with `Warning` `Strings.ExportOpenApiDuplicateOperation(method, path)`.
  - **operation** members in order: `summary` (request name), `operationId`, `tags` (when the request is inside a folder: `[ <folder names below the collection joined by " / "> ]`; requests directly under the collection have no `tags`), `parameters`, `requestBody` and `security` (M5-43), `responses`.
  - `operationId`: the request name split into words on non-alphanumeric characters, first word lower case, the others capitalised (`Get users` -> `getUsers`); empty -> `operation`; uniqueness: a repeated id gets `2`, `3` appended.
  - `responses`: always `{ "default": { "description": "Response" } }`.
  - **parameters**: first the path parameters (`{ "name", "in": "path", "required": true, "schema": { "type": "string" } }` for each `{name}` of the path, with `"example"` = the collection variable `name` when it has a non-empty value), then the query params (`{ "name", "in": "query", "required": <enabled>, "schema": { "type": "string" }, "example": value }`,
    `example` omitted when the value is empty), then the headers except `Content-Type`, `Accept` and `Authorization` (`in: header`, same shape, `required` = enabled).
- **tags** (top-level): one entry per folder that is used as a tag, in first-use order: `{ "name": <joined name>, "description": <folder description, omitted when empty> }`.
- Variables elsewhere in values are kept as written (`{{token}}`).
- Strings (EN and FR): `export.openApi.inferred`; `export.openApi.unresolvedServer` = "The base URL variable \"{0}\" has no value; the server was set to \"/\"."; `export.openApi.duplicateOperation` = "{0} {1} appears more than once; only the first request was exported.".

## Out of scope

Request bodies, security, the exporters themselves (M5-43), response schemas.

## Acceptance criteria

- [ ] Tests comparing `DataNodeJson.Write(compact)` with exact expected text: a collection with `{{baseUrl}}/users/{{id}}` (baseUrl variable set) and a literal `https://other.example.com/v2/items?x=1`; two servers in first-seen order; unresolved variable origin -> `/` + Warning; `:id` style path; operations of several methods under one path;
  duplicate operation skipped + Warning; operationId generation and uniqueness; tags from nested folders joined with ` / `; tag descriptions; query param required/optional with example; header filtering; path parameter example from a collection variable; the `Info`.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): infer OpenAPI paths and parameters from a collection`
