# M5-41: Core: Postman environment and globals export

Status: todo
Platform: any
Depends on: M5-05, M5-36

## Goal

`PostmanEnvironmentExporter` writes one Postman environment file per selected environment, and a globals file when globals are included.

## Design decisions

- Read first: M5-16 (the file shape, this is its inverse), M5-36.
- `src/Yaapic.Core/Export/Postman/PostmanEnvironmentExporter.cs`, `public sealed class PostmanEnvironmentExporter : IExporter`, `Id` `postman-environment`, `DisplayName` `Postman environment`, `FileExtension` `.json`, `RequiresCollection` false.
- Files: for each environment of `source.Environments` (in order): `ExportFile(UniquePath(SafeFileName(name) + ".postman_environment.json", used), bytes)`; when `source.Globals` is not empty: one more file `globals.postman_globals.json` (name `Globals`). No environment and no globals: `ExportException(Strings.ExportNothingToExport)`.
- File content (member order exactly): `{ "id": <environment id "D">, "name", "values": [ { "key", "value", "type": "default"|"secret", "enabled": true|false } ], "_postman_variable_scope": "environment"|"globals" }`; the globals file uses `name` `Globals` and, as `id`, the first 16 bytes of `SHA256.HashData("yaapic-globals" as UTF-8)` formatted as a GUID ("D"), so the file is deterministic.
  `type` is `secret` for `IsSecret` rows (value already blank), else `default`. No `_postman_exported_at` or `_postman_exported_using` members (not deterministic).
- Report: none beyond M5-36's secrets `Info`.
- Strings (EN and FR): `export.nothingToExport` = "There is nothing to export."

## Out of scope

Collection-scoped environments of other tools, Postman vault.

## Acceptance criteria

- [ ] Tests with exact JSON: two environments with default, secret and disabled rows; a globals file; both together give three files with unique names (two environments with the same name get `-2`); no data throws `ExportException`; deterministic globals id; no BOM, trailing `\n`.
- [ ] Round trip with `PostmanEnvironmentImporter` (M5-16): export then import gives equal rows and a `Globals` list; if M5-16 is not `done`, leave it to M5-66.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export environments and globals as Postman files`
