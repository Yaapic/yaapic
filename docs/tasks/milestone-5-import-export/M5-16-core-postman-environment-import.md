# M5-16: Core: Postman environment and globals import

Status: todo
Platform: any
Depends on: M5-04, M5-05

## Goal

`PostmanEnvironmentImporter` reads a Postman environment file into an `EnvironmentDefinition`, and a globals file into the globals.

## Design decisions

- Read first: ADR 0016 ("Bundle", secrets), ADR 0017 (Variables), `docs/file-format.md` (Environment file, Secret variables), M5-04, M5-05.
- `src/Yaapic.Core/Import/Postman/PostmanEnvironmentImporter.cs`, `public sealed class PostmanEnvironmentImporter : IImporter`, `Id` `postman-environment`, `DisplayName` `Postman environment or globals`.
- **Sniff**: `Certain` when the first 4096 characters contain `_postman_variable_scope`; `Possible` when they contain both `"values"` and `"name"` and not `"item"`; else `None`.
- Format: `{"id", "name", "values": [{"key", "value", "type": "default"|"secret"|"any", "enabled": true}], "_postman_variable_scope": "environment"|"globals"}`. Missing `values` array: `ImportException(Strings.ImportPostmanNotAnEnvironment)`.
- Rows: `KeyValueEntry(key, value as string or "", Enabled = enabled != false, IsSecret = type == "secret")`; elements without a key dropped; values are **not** dynamic-variable translated (they are data).
- Result: `_postman_variable_scope` equal to `globals` -> `ImportBundle` with `Globals = rows`, no environment. Otherwise one `EnvironmentDefinition(context.NewId(), SafeName(name, input.Name), rows, null)` in `Environments` (no collection scope: a Postman environment file does not name a collection).
  The bundle has no `Collection` and no unfiled requests. A secret row adds nothing to the report (the applier reports dropped secrets); a secret row whose `value` is not empty is kept as is (the secret-aware store moves it to the vault).
- Strings (EN and FR): `import.postman.notAnEnvironment` = "This file is not a Postman environment or globals file (no \"values\")."

## Out of scope

Exporting environments (M5-41), Postman vault references.

## Acceptance criteria

- [ ] Tests: sniff cases; an environment with default, secret and disabled rows (exact rows); a globals file lands in `Globals`; a missing `values` throws `ImportException`; a missing `name` falls back to the input name; non-string values (`1`, `true`) become text; rows without key dropped; no `Collection`.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Postman environments and globals`
