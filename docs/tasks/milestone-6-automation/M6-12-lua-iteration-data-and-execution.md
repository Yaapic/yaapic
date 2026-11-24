# M6-12: Lua: `yaapic.iteration_data` and `yaapic.execution`

Status: todo
Platform: any
Depends on: M6-10

## Goal

The Lua counterpart of M6-11.

## Design decisions

- Read first: M6-11 (same behaviour and messages), `src/Yaapic.Scripting/Lua/LuaPmVariablesPrelude.cs` (the
  `for _, name in ipairs({ 'execution', 'iterationData', ... })` stubs and the `iteration_data` alias), the Lua naming used by the
  variable scopes (method names and snake_case aliases), `tests/Yaapic.Scripting.Tests/LuaPmVariablesTests.cs`, the Lua conformance suite.
- Remove `execution` and `iterationData` from the Lua unsupported list. `yaapic.iterationData` (and `yaapic.iteration_data`): a read-only
  table (metatable `__newindex` error, as the existing read-only tables) with `get(name)` (string or `nil`), `has(name)`, `toObject()`
  and the snake_case alias `to_object()` (follow exactly the aliasing already used for `yaapic.variables`), `toJSON()`/`to_json()`
  returning the same table. Argument errors: `"yaapic.iterationData.get: name must be a string"`.
- `yaapic.execution` (read-only table): `setNextRequest(nameOrId)` and alias `set_next_request` (`nil` = stop the run), `skipRequest()`
  and alias `skip_request`, `location`/`runRequest` unsupported stubs with the existing helper.
- `yaapic.info.iteration` and `iterationCount` (plus the snake_case alias `iteration_count` if `info` already has snake_case aliases)
  from `info.get`.
- Key order of `toObject()`: Lua tables are unordered; document it in the XML doc of the prelude and do not test order.

## Out of scope

JavaScript (M6-11), the converter and autocompletion (M6-13).

## Acceptance criteria

- [ ] `Yaapic.Scripting.Tests` (real Lua, real `ScriptHost`): the same cases as M6-11, with the Lua aliases; mixed chains (a JavaScript
  collection script and a Lua request script) see the same row and the last `setNextRequest` wins.
- [ ] The Lua conformance suite moves the same rows from unsupported to supported; all green.
- [ ] 0 warnings, all tests green; the AOT smoke check still passes.

## Suggested commit message

`feat(scripting): add iterationData and execution to the Lua API`
