# M3-27: Lua `pm`: info, variables, environment, collection variables, globals

Status: done
Platform: any
Depends on: M3-26, M3-08

## Goal

The Lua twin of M3-18: same names and semantics, Lua idioms.

## Design decisions

- Read first: M3-18 (the exact list to mirror), M3-26 (`host`, `pm.null`), ADR 0009 (Lua idioms).
- Prelude part `PmVariables.lua`. `pm.variables`, `pm.environment`, `pm.collectionVariables`, `pm.globals` are tables of
  closures (no colon calls): `get(name)` -> string or `nil`, `has(name)`, `set(name, value)`, `unset(name)`, `clear()`,
  `to_object()` (a table `name -> value`; the JavaScript name `toObject` is **also** provided as an alias so Postman habits
  keep working: every JavaScript camelCase member has a Lua alias in camelCase **and** snake_case, the card lists them).
  `pm.variables.replace_in(text)` (alias `replaceIn`).
- Value coercion in `set`: string as is, number/boolean -> `tostring` (integers without `.0`: `math.type`), `nil` and `pm.null`
  -> `""`, table -> `pm.json.encode` (error text `"cannot store value of type function"` for functions).
- `pm.info = { event_name, request_name, request_id, iteration = 0, iteration_count = 1 }` plus camelCase aliases
  (`eventName`, ...).
- Unsupported members as in M3-18 (`pm.execution`, `pm.iterationData`, `pm.vault`, `pm.visualizer`, `pm.require`): a table with
  an `__index` raising `"pm.<path> is not supported in Yaapic"`. Legacy `postman.*` aliases are **not** provided in Lua (the
  API is new to Lua users).
- `pm` is protected: `pm` is a read-only proxy table (`__newindex` raises `"pm is read-only"`, `__metatable` set), so
  scripts cannot overwrite members; `rawset(_G, "pm", ...)` is possible and harmless (document: a script only hurts itself).
- Argument checks raise `bad argument #1 to 'get' (string expected, got number)`-style messages.

## Out of scope

Request/response, tests, `sendRequest`, cookies.

## Acceptance criteria

- [x] Tests (real Lua + real `ScriptHost`/`ScriptVariableOverlay`): the same table as M3-18 translated to Lua (get/set/unset/
      has/clear/to_object per scope, coercions including integer vs float formatting, layered precedence, `replace_in`
      with dynamic and unresolved names, missing-environment fallback, `info` in both events, unsupported messages, `pm`
      immutability, shared overlay between two blocks).
- [x] 0 warnings, all tests green, AOT smoke `OK`.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(scripting): add pm variables and info to the Lua engine`
