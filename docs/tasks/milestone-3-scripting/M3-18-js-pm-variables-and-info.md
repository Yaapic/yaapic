# M3-18: JavaScript `pm`: info, variables, environment, collection variables, globals

Status: todo
Platform: any
Depends on: M3-17, M3-08

## Goal

The first part of the JavaScript `pm` prelude: variable scopes, `pm.info`, `pm.variables.replaceIn`, the legacy
`postman.*` aliases and the "not supported" stubs.

## Design decisions

- Read first: ADR 0009 (`pm` surface, unsupported list), M3-14 (`HostOps`, DTOs), M3-17 (`JsPrelude.Build()`),
  Postman's public scripting reference for the exact semantics (property names, return values).
- Prelude file `src/Yaapic.Scripting/QuickJs/Prelude/PmVariables.js` is **embedded as a C# `const string`** (a
  `PmVariablesPrelude.cs` file holding the text in a raw string literal), never loaded at run time from disk, no
  `EmbeddedResource` reflection. `JsPrelude.Build()` concatenates the parts in order: core, variables, request/response
  (M3-19), test/expect (M3-20, M3-21), sendRequest/cookies (M3-22). Each part is evaluated as its own script.
- `pm.variables`, `pm.environment`, `pm.collectionVariables`, `pm.globals` are instances of one `VariableScope` class in the
  prelude bound to a scope name (`layered`, `environment`, `collection`, `globals`):
  - `get(name)` -> string or `undefined`; `has(name)` -> boolean; `set(name, value)` -> undefined (coerce: string as is,
    number/boolean/bigint with `String`, `null`/`undefined` -> `""`, anything else `JSON.stringify`, which never throws:
    circular -> `"[object Object]"`); `unset(name)`; `clear()`; `toObject()` -> plain object of the enabled rows.
  - `pm.variables.set` writes the **local** layer (host scope `local`), `pm.variables.get/has/toObject` read the layered
    view; `pm.variables.replaceIn(text)` -> `var.replace`; `pm.variables.unset/clear` act on local only.
  - Argument checks: a non-string `name` throws `TypeError("pm.environment.get: name must be a string")` style messages.
- `pm.info`: `eventName` (`prerequest`/`test`), `requestName`, `requestId`, `iteration` 0, `iterationCount` 1 from `info.get`.
- Legacy aliases: `postman.setEnvironmentVariable(k, v)`, `getEnvironmentVariable(k)`, `clearEnvironmentVariable(k)`,
  `setGlobalVariable`, `getGlobalVariable`, `clearGlobalVariable`.
- Unsupported stubs, all throwing `Error("pm.<path> is not supported in Yaapic")` **on access of a member** (use a
  `Proxy` per stub): `pm.execution`, `pm.iterationData`, `pm.vault`, `pm.visualizer`, `pm.require`, `pm.environment.name`
  (not exposed in v1), `postman.setNextRequest`, `postman.visualizer`. `pm.sendRequest`, `pm.cookies`, `pm.request`,
  `pm.response`, `pm.test`, `pm.expect` come from later cards (until then undefined).
- Scripts cannot replace `pm`: define it with `Object.defineProperty(globalThis, "pm", {value, writable:false,
  configurable:false})` and freeze the prototypes.

## Out of scope

Request/response objects, tests, `sendRequest`, cookies, secrets (they are ordinary rows to scripts, ADR 0010).

## Acceptance criteria

- [ ] Tests (`Yaapic.Scripting.Tests`, real QuickJS + the real `ScriptHost` with `ScriptVariableOverlay`, so M3-08 and the
      prelude are exercised together): get/set/unset/has/clear/toObject for each scope; coercion table; layered precedence
      (local > environment > collection > globals); `replaceIn` with `{{name}}`, dynamic `{{$guid}}` and unresolved names;
      writes to a missing environment fall back to local (value visible, warning in the overlay); `pm.info` fields in both
      event names; legacy aliases; every unsupported stub throws the exact message; `pm = 1` and `delete globalThis.pm` do
      not replace it; a second block in the same run sees the first block's changes (shared overlay).
- [ ] 0 warnings, all tests green; the AOT smoke tool still prints `OK`.

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

`feat(scripting): add pm variables and info to the JavaScript engine`
