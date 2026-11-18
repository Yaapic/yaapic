# M5-13: Core: Postman import, request bodies

Status: done
Platform: any
Depends on: M5-12

## Goal

The Postman `request.body` becomes a Yaapic `RequestBody`.

## Design decisions

- Read first: M5-12, `src/Yaapic.Domain/Http/RequestBody.cs` (factories) and `FormField.cs`, `FormFieldKind.cs`, M5-06 (`ContentTypeFromMime`).
- `src/Yaapic.Core/Import/Postman/PostmanBodies.cs` (internal static): `RequestBody Map(DataNode? body, string path, ImportReport report)`; called by `PostmanItems` for every request. `body` absent, null, or `mode` absent/`null` -> `RequestBody.None`.
- By `body.mode`:

  | `mode` | Mapping |
  |---|---|
  | `raw` | `Kind Raw`, `Text = TranslateDynamicVariables(body.raw)`, `ContentType` from `body.options.raw.language`: `json` Json, `xml` Xml, `html` Html, `javascript` JavaScript, anything else or absent Text. When `options` is absent and the request has a `Content-Type` header, use `ContentTypeFromMime(header value)` (the header is passed in by the caller). |
  | `urlencoded` | `Kind UrlEncoded`, `Fields` from `body.urlencoded` array: `FormField(key, value ?? "", Enabled = !disabled, Text)`; entries without a key dropped. |
  | `formdata` | `Kind FormData`; each element: `type` `file` -> `FormField(key, src, Enabled = !disabled, File)` where `src` is a string or the first string of an array (empty or missing `src`: the field is dropped and a `Warning` `Strings.ImportPostmanFileMissing(path, key)`); otherwise a `Text` field with `value`. `contentType` of a part is ignored. |
  | `file` | `Kind Binary`, `FilePath = body.file.src` (empty: `RequestBody.None` and the same missing-file `Warning`). |
  | `graphql` | `Kind Raw`, `Json`, `Text` = compact JSON object `{"query": <body.graphql.query>, "variables": <parsed body.graphql.variables as JSON, or {} when empty or invalid>}` built with `DataNodeJson.Write(compact)`; add `Info` `Strings.ImportPostmanGraphQl`. If the request has no `Content-Type` header, the caller adds `Content-Type: application/json` (the editor does not add one for you: pass a flag back through the return value `(RequestBody Body, string? AddedContentType)`). |
  | other | `RequestBody.None` and `Warning` `Strings.ImportPostmanBodyModeUnsupported(path, mode)`. |

- **File references** (`formdata` file fields and `file` mode): one `Warning` per reference, `Strings.ImportFileReference(path, filePath)` ("This request reads the local file \"{1}\" when it is sent. Check the path.").
- Strings (EN and FR): `import.postman.fileMissing` = "The file field \"{1}\" has no path and was dropped."; `import.postman.graphQl` = "The GraphQL body was converted to a JSON body."; `import.postman.bodyModeUnsupported` = "The body mode \"{1}\" is not supported; the request has no body.";
  `import.fileReference` (shared by every importer).

## Out of scope

Auth, scripts, variables.

## Acceptance criteria

- [x] Tests (`PostmanBodiesTests` and importer-level): each mode with exact expected `RequestBody` values, the language mapping including the header fallback, a disabled urlencoded field, a file field with `src` string and array, a file field without `src` (dropped + Warning), `file` mode, GraphQL with and without variables and
  with invalid variables, an unknown mode, `$randomUUID` in a raw body, the file-reference Warning count, the added `Content-Type` for GraphQL only when absent.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Postman request bodies`
