# M6-13: Converter, autocompletion and snippets for the runner APIs

Status: done
Platform: any
Depends on: M6-11, M6-12

## Goal

The script converter, the editor autocompletion and the snippets know the new runner APIs; the compatibility doc says they work.

## Design decisions

- Read first: `src/Yaapic.Core/Scripting/ScriptConverter.cs` (`PostmanUnsupported`, `PostmanLegacy`, `Bruno`),
  `src/Yaapic.UI.Abstractions/Editing/ScriptApiCatalogue.cs` (the `info` entry near line 197) and `ScriptSnippetCatalogue.cs`,
  `docs/scripting-compatibility.md`, their tests (`rtk grep -rln "ScriptConverter\|ScriptApiCatalogue\|ScriptSnippetCatalogue" tests`).
- `ScriptConverter`:
  - remove `pm.execution` and `pm.iterationData` from `PostmanUnsupported` (they now map to `yaapic.execution` and
    `yaapic.iterationData` by the generic `pm.` renaming); `pm.execution.location` and `pm.execution.runRequest` stay reported as
    "no yaapic equivalent";
  - `PostmanLegacy` gains `["postman.setNextRequest"] = "yaapic.execution.setNextRequest"`;
  - `Bruno`: `["bru.setNextRequest"] = new("yaapic.execution.setNextRequest")`, `["bru.runner.skipRequest"] = new("yaapic.execution.skipRequest")`,
    `["bru.runner.stopExecution"] = new(null, "use yaapic.execution.setNextRequest(null)")` (a finding, not rewritten: the `Mapping`
    record cannot add an argument).
  - the line-316 special case "needs the Runner (milestone 6)" is removed.
- `ScriptApiCatalogue`: add `iterationData` (Property; members `get(name)`, `has(name)`, `toObject()`, `toJSON()`; Lua name
  `iteration_data` and the Lua aliases of M6-12) and `execution` (Property; members `setNextRequest(nameOrId)` with Lua name
  `set_next_request` only if M6-12 added it, `skipRequest()`), and the `info` members `iteration`, `iterationCount`. Descriptions in English:
  "The current row of the Runner data file.", "Choose the next request of the Runner run; null stops the run.",
  "Skip this request (pre-request scripts only).", "The current iteration (0-based).", "The number of iterations of the run.".
- `ScriptSnippetCatalogue`: two snippets, any language: `nextRequest` "Go to another request in the Runner" (JavaScript
  `yaapic.execution.setNextRequest("Request name");`, Lua `yaapic.execution.setNextRequest("Request name")`) and `iterationValue`
  "Read a value of the data file" (`const value = yaapic.iterationData.get("column");` / `local value = yaapic.iterationData.get("column")`).
- `docs/scripting-compatibility.md`: the `postman.setNextRequest`, `yaapic.execution` and `yaapic.iterationData` rows become
  "supported (Runner)" with one line each on the behaviour of ADR 0018; the unsupported list keeps `yaapic.execution.location` and
  `yaapic.execution.runRequest`.

## Out of scope

The runner itself, the engines (done).

## Acceptance criteria

- [x] Converter tests: Postman `pm.iterationData.get('x')`, `pm.execution.setNextRequest('B')`, `postman.setNextRequest(null)`,
  `pm.execution.skipRequest()` convert with no finding; Bruno `bru.setNextRequest('B')` converts; `pm.execution.location` still reported.
- [x] Catalogue tests: completion after `yaapic.` lists `iterationData` and `execution`; after `yaapic.info.` lists `iteration`; Lua names
  match the prelude (assert against the real Lua prelude through an existing catalogue-vs-prelude test if there is one).
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(scripting): convert and complete the runner script APIs`
