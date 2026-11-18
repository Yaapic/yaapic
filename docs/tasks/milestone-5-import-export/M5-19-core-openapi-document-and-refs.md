# M5-19: Core: OpenAPI document reading and `$ref` resolution

Status: done
Platform: any
Depends on: M5-05

## Goal

The OpenAPI importer shell: detect and parse a 3.x document (JSON or YAML) and resolve local `$ref`s, so the next cards read operations without handling references.

## Design decisions

- Read first: ADR 0016 (untrusted files), ADR 0017 (OpenAPI), M5-05 (`DataNode`, `StructuredTextReader`).
- Files in `src/Yaapic.Core/Import/OpenApi/`: `OpenApiImporter.cs` (`public sealed class OpenApiImporter(StructuredTextReader reader) : IImporter`, `Id` `openapi`, `DisplayName` `OpenAPI`; its `Import` is completed by M5-20 to M5-24), `OpenApiRefResolver.cs` (internal).
- **Sniff**: `Certain` when the first 4096 characters match (use `[GeneratedRegex]`, multiline, ignore case) `^\s*["']?(openapi|swagger)["']?\s*:\s*["']?(3\.[0-9]|2\.0)`; `Possible` when they contain `"paths"` or a line starting `paths:`; else `None`.
- **Import (this card)**: `reader.Parse(input.Single.Text)` (a `FormatException` becomes `ImportException(message)`); the root must be an object with `openapi` (a string starting `3.`) or `swagger` (`2.0`, handled by M5-24) and a `paths` object (it may be empty), otherwise `ImportException(Strings.ImportOpenApiNotADocument)`.
  Versions `3.0.x` and `3.1.x` accepted; any other `3.x` accepted with an `Info`; `4.x` and unknown: `ImportException(Strings.ImportOpenApiVersion(version))`. Until M5-20 the importer returns an empty bundle with a collection folder named `info.title` (`SafeName`, fallback `input.Name`) and `info.description`.
- `OpenApiRefResolver` (`internal sealed class`, constructed with the root `DataNode` and the `ImportReport`):
  - `DataNode Resolve(DataNode node, string path)`: when `node` is an object with a string `$ref`: a ref starting `#/` is a JSON pointer into the root (split on `/`, decode `~1` to `/`, `~0` to `~`, then percent-decode); the target is resolved again (chains followed); sibling keys of `$ref` are ignored (3.0 rule).
    Any other ref (file path, URL) -> `Warning` `Strings.ImportOpenApiExternalRef(path, ref)` and `DataNode.Null`. A missing target -> `Warning` `Strings.ImportOpenApiBrokenRef(path, ref)` and `DataNode.Null`. A ref chain longer than 20 or a ref cycle (a ref already being resolved) -> `Warning` `Strings.ImportOpenApiRefCycle(path, ref)` and Null.
  - `DataNode? Get(DataNode obj, string key, string path)` = resolve of `obj[key]`.
  - It never copies the document (resolution returns the target node itself; nodes are immutable).
- Strings (EN and FR): `import.openapi.notADocument` = "This file is not an OpenAPI document (no \"openapi\" version or \"paths\")."; `import.openapi.version` = "OpenAPI version {0} is not supported."; `import.openapi.externalRef` = "{0}: the external reference \"{1}\" is not followed.";
  `import.openapi.brokenRef` = "{0}: the reference \"{1}\" does not exist."; `import.openapi.refCycle` = "{0}: the reference \"{1}\" is circular.".

## Out of scope

Operations (M5-20), Swagger 2.0 (M5-24).

## Acceptance criteria

- [x] Tests: sniff for JSON and YAML 3.0, 3.1, Swagger 2.0, a file with only `paths`, an unrelated JSON; parse with a fake `IYamlTreeReader` for YAML input; missing `paths` throws; `4.0.0` throws; collection name from `info.title` with fallback; `$ref` cases: simple, nested chain, pointer with `~1` and `%20`, external file ref, URL ref, missing target,
  cycle (A refers to B refers to A), sibling keys ignored, a non-ref node returned as is.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): read OpenAPI documents and resolve local references`
