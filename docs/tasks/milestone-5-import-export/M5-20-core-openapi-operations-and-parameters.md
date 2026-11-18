# M5-20: Core: OpenAPI import, operations, servers and parameters

Status: done
Platform: any
Depends on: M5-06, M5-19

## Goal

Every path operation becomes a request in a folder per tag, with `{{baseUrl}}`, path variables, query and header parameters. Bodies (M5-22) and security (M5-23) come next.

## Design decisions

- Read first: ADR 0017 (Variables, OpenAPI), M5-06, M5-19, `docs/file-format.md`.
- `src/Yaapic.Core/Import/OpenApi/OpenApiOperations.cs` (internal static); `OpenApiImporter.Import` calls it.
- **Servers**: `servers` array of `{url, description, variables}`. The url has its `{var}` placeholders replaced by the server variable `default` (missing: left as written + `Warning` `Strings.ImportOpenApiServerVariable`). A relative url (`/api/v1`) is kept as is, and `Info` `Strings.ImportOpenApiRelativeServer(url)` says to set `baseUrl`.
  No `servers`: `baseUrl = ""` with the same `Info`. First server -> collection variable `baseUrl` (`KeyValueEntry("baseUrl", url)`); each further server -> an `EnvironmentDefinition(NewId, SafeName(description, url), [baseUrl = url], CollectionId = collection folder id)`.
  A trailing `/` is removed from the url. `servers` on a path item or an operation are ignored with one `Info` per import (`Strings.ImportOpenApiLocalServers`).
- **Operations**: for each path in `paths` (document order) and each key among `get put post delete options head patch` (that order; `trace` and unknown keys: `Skipped` entry; `$ref` on a path item resolved with the ref resolver):
  - `Name = SafeName(summary, operationId, else "<METHOD> <path>")` (summary first, then operationId, then the fallback).
  - **URL** = `{{baseUrl}}` + the path with each `{name}` replaced by `{{name}}`. The `Url` is stored without query.
  - **Folder**: `tags[0]` of the operation names the folder (`SafeName`); folders are created on first use as direct children of the collection folder, `Description` from the top-level `tags` entry with that `name` (`description`); no tags: the request is a direct child of the collection folder.
  - **Parameters**: the path item's `parameters` and the operation's `parameters` merged, an operation entry replacing a path item entry with the same `(in, name)`; every entry resolved with the ref resolver.
    - `in: query` -> `KeyValueEntry(name, sampleValue, Enabled = required == true)`; `in: header` -> header with the same rule, **except** the names `Accept`, `Content-Type` and `Authorization` (ignored per the OpenAPI spec, no entry); `in: path` -> a collection variable `name` (added once per name, value = sample value) so the `{{name}}` resolves; `in: cookie` -> ignored, `Info` aggregated `Strings.ImportOpenApiCookiesIgnored(count)`.
    - **sampleValue**: `example` of the parameter; else the first value of its `examples` (`value` member); else `schema.example`; else `schema.default`; else the first `schema.enum` value; else `""`. Scalar values are written with `DataNode.AsString`; an object or array value is written as compact JSON (`DataNodeJson.Write(node, false)`).
    - A query parameter with `style: form, explode: true` and an array example is written once as comma-joined text (no explosion); no special handling for `deepObject`.
  - Operation `deprecated: true` adds ` (deprecated)` to nothing: keep the name and add an `Info` `Strings.ImportOpenApiDeprecated(path)`.
  - Operation `description` is dropped (counted for the shared `Strings.ImportDescriptionsDropped`).
- **Collection folder**: name `SafeName(info.title, input.Name)`, `Description` `info.description`; `Variables` = `baseUrl` then the path variables in first-use order.
- Result `Auth`, `Body`, `Settings`, `Scripts`: defaults (until M5-22 and M5-23).
- Strings (EN and FR): `import.openapi.serverVariable` = "Server variable {0} has no default; the URL keeps the placeholder."; `import.openapi.relativeServer` = "The server URL \"{0}\" is relative: set the baseUrl variable of the collection before sending.";
  `import.openapi.localServers` = "Servers declared on paths or operations are ignored; the first global server is used."; `import.openapi.cookiesIgnored` = "{0} cookie parameters were ignored."; `import.openapi.deprecated` = "{0} is marked deprecated in the source."

## Out of scope

Bodies, security, schemas, Swagger 2.0, responses (never imported).

## Acceptance criteria

- [x] Tests with a YAML-free inline JSON document and `SequentialIds`: two tags and an untagged operation (folder structure, names, descriptions of folders); name fallbacks (summary, operationId, method+path); URL with `{id}` and the `id` collection variable; one and three servers (variable `baseUrl`, two scoped environments with names);
  server variables with and without default; relative server; query parameters required (enabled) and optional (disabled), sample value sources in the stated order, header params and the three ignored names, path-level parameters overridden by operation-level ones, `$ref` parameters from `components/parameters`, cookie params counted, `trace` skipped,
  deprecated `Info`, `$ref` path item, an empty `paths` (empty bundle with only the collection folder).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import OpenAPI operations, servers and parameters`
