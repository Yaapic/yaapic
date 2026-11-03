# M2-27: YAML folder serializer

Status: done
Platform: any
Depends on: M2-25

## Goal

Serialize `FolderDefinition` to and from `folder.yaml` (ADR 0008, `docs/file-format.md`).

## Design decisions

- Same pattern as `RequestYamlSerializer` (M1-12): `internal partial class FolderFileDto` with
  `[YamlObject(NamingConvention.LowerCamelCase)]` (`Version`, `Id`, `Name`, `Description`, `Variables` as
  `List<KeyValueDto>`), and `public static class FolderYamlSerializer { string Serialize(FolderDefinition);
  FolderDefinition Deserialize(string yaml); }`.
- Serialize: keys in the order of the format, every key written, `\n` line endings, one trailing newline, a multi-line
  description as a literal block scalar.
- Deserialize: `version` must be 1 (`StorageException("Unsupported folder file version: X")`); invalid YAML, missing or
  invalid `id` -> `StorageException`; missing `name` -> `"Untitled"`; missing `description` -> `""`; missing
  `variables` -> empty; unknown keys ignored.
- Golden file `tests/Yaapic.Infrastructure.Tests/Storage/golden/folder-v1.folder.yaml`.

## Out of scope

Disk access (M2-28 onwards).

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests`: round trip equivalence with variables, unicode and a multi-line
      description; golden match byte for byte; defaults for missing keys; unknown key ignored; version 2 and malformed YAML
      throw `StorageException`.
- [x] 0 warnings (AOT analyzers on).

## Suggested commit message

`feat(infrastructure): add YAML folder serializer`
