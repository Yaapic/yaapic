# M6-11: JavaScript: `yaapic.iterationData` and `yaapic.execution`

Status: done
Platform: any
Depends on: M6-10

## Goal

Replace the JavaScript "not supported" stubs of `yaapic.iterationData`, `yaapic.execution` and `postman.setNextRequest` with the
ADR 0018 API, and make `yaapic.info.iteration`/`iterationCount` real.

## Design decisions

- Read first: ADR 0018 ("Script API"), M6-10, `src/Yaapic.Scripting/QuickJs/PmVariablesPrelude.cs` (stubs at the `for (const name of
  ['execution', 'iterationData', ...])` loop and `postman.setNextRequest`), how `yaapic.info` is built from `info.get`, how the `pm`
  alias is defined (U-02), `tests/Yaapic.Scripting.Tests/PmVariablesTests.cs`, the conformance suite (`tests/Yaapic.Scripting.Tests/Conformance/`).
- In `PmVariablesPrelude`:
  - remove `execution` and `iterationData` from the unsupported list (keep `vault`, `visualizer`, `require`);
  - `yaapic.iterationData`: a frozen object with `get(name)` (string or `undefined`; non-string name throws
    `TypeError("yaapic.iterationData.get: name must be a string")`), `has(name)`, `toObject()` (plain object, insertion order),
    `toJSON()` (same as `toObject`). The row is read once per script run through `__host('iter.get')` and cached;
  - `yaapic.execution`: a frozen object with `setNextRequest(nameOrId)` (accepts a string, `null` or `undefined` (= `null`); a GUID
    string is passed as is; other types throw `TypeError("yaapic.execution.setNextRequest: expected a request name, id or null")`),
    `skipRequest()`, and `location`, `runRequest` as unsupported stubs (same `unsupported(...)` helper and message format);
  - `postman.setNextRequest` = `yaapic.execution.setNextRequest`;
  - `yaapic.info.iteration` and `yaapic.info.iterationCount` from `info.get` (they were constants).
  `pm.*` sees the same objects (alias).
- After `skipRequest()` the script keeps running to its end (Postman behaviour); the sender stops after the block (M6-09).

## Out of scope

Lua (M6-12), the converter and autocompletion (M6-13).

## Acceptance criteria

- [x] `Yaapic.Scripting.Tests` (real QuickJS, real `ScriptHost`): `iterationData` get/has/toObject/toJSON with a row and with
  the empty row; argument errors; `setNextRequest` with a name, a GUID, `null` and no argument, and outside a run (warning in the
  report); `skipRequest` in pre-request (report `Skipped`) and in post-response (script error with the M6-09 message);
  `postman.setNextRequest` and `pm.execution` aliases; `info.iteration`/`iterationCount` inside and outside a run;
  `execution.location` throws the unsupported message.
- [x] The conformance suite: the rows for `pm.iterationData`, `pm.execution.setNextRequest`, `pm.execution.skipRequest` and
  `postman.setNextRequest` move from "unsupported" to supported cases; still all green.
- [x] 0 warnings, all tests green; the AOT smoke check still passes.

## Suggested commit message

`feat(scripting): add iterationData and execution to the JavaScript API`
