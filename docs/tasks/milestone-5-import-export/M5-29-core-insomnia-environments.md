# M5-29: Core: Insomnia import, environments

Status: todo
Platform: any
Depends on: M5-27, M5-28

## Goal

Insomnia environments become collection variables (base environment) and collection-scoped environments (sub environments).

## Design decisions

- Read first: M5-27, M5-28 (`InsomniaTemplates`), ADR 0017 (Variables), `src/Yaapic.Domain/Environments/EnvironmentDefinition.cs`.
- `src/Yaapic.Core/Import/Insomnia/InsomniaEnvironments.cs` (internal static), called by `InsomniaImporter`.
- Structure in the export: resources of `_type` `environment` with `parentId` = the workspace `_id` are **base environments** (normally one, `name` "Base Environment"); resources with `parentId` = a base environment `_id` are **sub environments**. Each has `data` (an object) and `name`. `isPrivate` is ignored.
- **Base environment** -> the collection folder's `Variables` (flattened rows of its `data`). Several base environments: merge in resource order, a later key replacing an earlier one. **Sub environments** -> `EnvironmentDefinition(NewId, SafeName(name, "Environment"), rows, CollectionId = collection folder id)` appended to `Environments` (in resource order).
- **Flattening** a `data` object into rows `KeyValueEntry(key, value, Enabled = true)`: members in order; a String value is `Translate`d (M5-28) and kept; Number and Bool use `AsString`; Null -> `""`; an Object value is flattened recursively with keys joined by `.` (`{"api": {"host": "x"}}` -> `api.host` = `x`) up to depth 4, deeper objects and all Arrays are written as compact JSON text
  (`DataNodeJson.Write(node, false)`). Row keys are kept as written (no sanitizing). A sub environment inherits nothing from the base in Yaapic: collection variables stay visible because the sub environment is scoped to the same collection and variable precedence is environment first, then collection.
- Environments whose `parentId` matches nothing are attached as sub environments of the collection. An environment with an empty `data` is still imported (empty variable list).
- No secrets: Insomnia has no secret flag (a private environment is still plain data).
- Strings: none new.

## Out of scope

Insomnia vault, `isPrivate` handling, cookie jars.

## Acceptance criteria

- [ ] Tests: base environment variables land in the collection folder (exact rows, including a flattened `api.host`, a number, a boolean, null, an array as JSON, a template string translated); two sub environments scoped to the collection id in order; two base environments merged with a replaced key; an orphan environment attached;
  empty `data`; depth-5 object written as JSON at the limit; the importer end to end on a small export with workspace, folder, request, base and sub environment.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Insomnia environments`
