# M5-35: Core: Bruno import, environments

Status: done
Platform: any
Depends on: M5-31, M5-33

## Goal

`environments/*.bru` files become environments scoped to the imported collection, with secret variables.

## Design decisions

- Read first: M5-31, M5-33 (the walk skips `environments/`), ADR 0017 (Variables), `docs/file-format.md` (Secret variables).
- `src/Yaapic.Core/Import/Bruno/BrunoEnvironments.cs` (internal static): `IReadOnlyList<EnvironmentDefinition> Map(ImportInput input, string rootPath, Guid collectionId, Func<Guid> newId, ImportReport report)`; `BrunoImporter` appends the result to `Environments`.
- An environment file `environments/<name>.bru` has a `vars` dictionary block (`name: value`, `~` prefix disabled) and an optional `vars:secret [ a, b ]` list block of secret variable **names** (Bruno never stores secret values in the file).
- `EnvironmentDefinition(newId(), name, rows, CollectionId = collectionId)` where `name` = the file name without `.bru`; rows = `vars` pairs in order (`KeyValueEntry(key, value, Enabled)`), then one row `KeyValueEntry(name, "", Enabled = true, IsSecret = true)` per secret name (a name that is also in `vars` is flagged secret in place, its value cleared).
  When secret rows exist, the report gets one `Info` per import `Strings.ImportBrunoSecretsEmpty(count)` ("{0} secret variables were created empty: Bruno does not export their values.").
  Bruno `{{process.env.X}}` in values: left, warned once as in M5-32. An empty environment file still imports. Files in `environments/` that fail to read are ignored.
- Environments are imported only when a collection exists (a lone `.bru` request has no environments).
- Strings (EN and FR): `import.bruno.secretsEmpty`.

## Out of scope

Global environments (Bruno app-level), dotenv files.

## Acceptance criteria

- [x] Tests: two environments (names from file names, exact rows, scope = the collection folder id); a disabled var; `vars:secret` with a name absent from `vars` (new empty secret row) and one present (flagged in place); the `Info` count; an empty file; an environment file with CRLF; a collection without an `environments` directory; end to end with `BrunoImporter` (the environments land in the bundle).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Bruno environments`
