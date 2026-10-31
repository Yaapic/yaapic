# M1-14: Environment YAML and file environment store

Status: done
Platform: any
Depends on: M1-13

## Goal

`IEnvironmentStore` on `<workspace>/environments/*.env.yaml`.

## Design decisions

- `public static class EnvironmentYamlSerializer` (`Serialize` / `Deserialize`), same rules as `RequestYamlSerializer`
  (version 1, keys `version`, `id`, `name`, `variables`, DTOs internal, reuse `KeyValueDto`, missing `name` ->
  `"Untitled"`, `StorageException` on errors).
- `public sealed class FileEnvironmentStore(string workspaceRoot) : IEnvironmentStore`, directory `environments`, file
  suffix `.env.yaml`, slug fallback `environment`. Same behaviour as `FileRequestStore` (listing errors, id map,
  collision suffix, rename, atomic write, delete). Factor the shared listing/save logic only if it stays simpler than two
  short classes; do not change `FileRequestStore` behaviour or its tests.
- Golden file `tests/Yaapic.Infrastructure.Tests/Storage/golden/env-v1.env.yaml`.

## Out of scope

Secret variables, globals, environment import/export.

## Acceptance criteria

- [x] Serializer tests: round trip, golden file, version and malformed errors.
- [x] Store tests mirroring the M1-13 list (save/list/overwrite/rename/collision/delete/corrupt file).
- [x] 0 warnings, M1-13 tests still green.

## Suggested commit message

`feat(infrastructure): add file environment store`
