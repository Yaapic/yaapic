# M1-12: YAML request serializer

Status: done
Platform: any
Depends on: M1-10, M1-11

## Goal

Serialize `HttpRequestDefinition` to and from the request file format of `docs/file-format.md`.

## Design decisions

- Namespace `Yaapic.Infrastructure.Storage`, folder `src/Yaapic.Infrastructure/Storage/`.
- DTOs are `internal partial class` with `[YamlObject(NamingConvention.CamelCase)]` (or explicit `[YamlMember("...")]`
  names), holding strings, bools and lists only: `RequestFileDto`, `KeyValueDto`, `BodyDto`. Enums are stored as
  strings and converted by hand (`GET`/`POST`, `none`/`raw`, `text`/`json`/`xml`), so no enum formatter is needed.
  Domain types stay free of YAML attributes.
- `public static class RequestYamlSerializer { public static string Serialize(HttpRequestDefinition request);
  public static HttpRequestDefinition Deserialize(string yaml); }`.
- Serialize: keys in the documented order, every key written, `\n` line endings (normalise the output if VYaml emits
  `\r\n`), ends with exactly one `\n`.
- Deserialize: `version` must be 1, else `StorageException("Unsupported request file version: X")`; invalid YAML, a
  missing or invalid `id`, an unknown method/body type/content type -> `StorageException` with the reason; missing
  `name` -> `"Untitled"`; missing lists -> empty; missing `enabled` -> true; missing `body` -> `RequestBody.None`;
  unknown keys ignored (if VYaml rejects unknown members, configure it or pre-filter so they are ignored, and note how in
  `docs/file-format.md`).
- Golden file `tests/Yaapic.Infrastructure.Tests/Storage/golden/request-v1.request.yaml` committed; the serializer output
  for the matching fixture must equal it byte for byte.

## Out of scope

File system access (M1-13), environments, migrations.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests`: round trip keeps equivalence (`IsEquivalentTo`) for a request with params,
      headers, disabled entries, multi-line JSON body, unicode and `{{variables}}`; golden file match; unknown key ignored;
      defaults for missing optional keys; version 2 and malformed YAML throw `StorageException`.
- [x] 0 warnings (AOT analyzers on).

## Suggested commit message

`feat(infrastructure): add YAML request serializer`
