# M3-01: ADR 0009: scripting architecture, host protocol and file format

Status: done
Platform: any
Depends on: none

## Goal

Write ADR 0009, the single reference every other scripting card reads first. Docs only: no code. It records the
decisions below (they are final; the card only has to write them down precisely and update the docs listed).

## Design decisions (to record in the ADR)

- **Port and adapters.** `IScriptEngine` and everything an engine needs from the application (`IScriptHost`,
  `ScriptInvocation`, `ScriptLimits`) live in `Yaapic.Core.Scripting` (ADR 0001 says "port in `Yaapic.Core`"; the older
  project memory said `Yaapic.Scripting`: **the port is in Core, the adapters are in the new project `Yaapic.Scripting`**).
  `Yaapic.Scripting` references `Yaapic.Core` and `Yaapic.Domain` only (rule added to `DependencyRulesTests` by M3-14);
  `Yaapic.Composition` references it; ViewModels never do.
- **Engines.** QuickJS-NG (JavaScript) and Lua 5.4, bound with source-generated `LibraryImport` P/Invokes only. No
  reflection, no `DynamicDependency`, no runtime code generation. One native function is exposed to scripts,
  `__host(op, argsJson) -> resultJson`; everything else (`pm`, `console`, `expect`) is a prelude written in the script
  language and embedded as a C# `const string` per engine. The bridge decodes `argsJson` with `System.Text.Json` source
  generators (`HostJsonContext`) and calls the typed `IScriptHost`. The result is `{"ok":<json>}` or `{"err":"<message>"}`;
  the prelude throws on `err`. The op names are constants in `HostOps` (M3-14).
- **Scripts per owner.** A request and a folder (a top-level folder is the collection) each own a `ScriptSet` with one
  pre-request block and one post-response block; each block has its own language (`javascript` or `lua`, default
  `javascript`) and source text. The language is per block, so a collection can use JavaScript while one request uses Lua.
- **Execution order (Postman order).** Pre-request: collection, then each folder from the outermost to the innermost, then
  the request. Post-response: the same order (collection first). An empty block (blank source) is skipped.
- **What scripts see.** Pre-request scripts see the request **definition as written** (URL, headers, body keep their
  `{{variables}}`); `pm.variables.replaceIn(text)` resolves a string. The sender resolves the (possibly mutated)
  definition afterwards, so a variable set by a pre-request script is visible to `{{name}}` in the same send. Post-response
  scripts additionally see the response (read-only) and the request as it was sent.
- **Variable layers for scripts**, highest first: local (`pm.variables.set`, lives for one send), environment, collection,
  globals, dynamic (read-only). `pm.environment.set`, `pm.collectionVariables.set`, `pm.globals.set` write to the layered
  overlay at once and are persisted after the whole chain ran (through the existing stores, M3-44). When the scope does not
  exist (no active environment, request outside a collection) the value lives in the local layer for that send and a
  `warn` console line says so.
- **Error policy.** A script error (exception, timeout, memory limit) in a pre-request block **aborts the send**: the
  response panel shows the error with origin, line and message, nothing is sent and the variable changes made before the
  error are discarded. An error in a post-response block never discards the response: it is shown next to the response,
  the tests of that block already recorded are kept. A failed `pm.test` is a test result, not a script error.
- **Limits** (`ScriptLimits`, one record, defaults): 5 s wall-clock per block, 64 MiB memory per run, 20
  `pm.sendRequest` calls per run, 1 000 console entries per send, 8 192 characters per console entry (truncated with
  an ellipsis). Cancelling the send cancels the script.
- **Sandbox.** No file system, no `require`/`import` of host modules, no `process`, no timers, no network except
  `pm.sendRequest` (host-mediated, uses the app transport). JavaScript: QuickJS core only (no `std`/`os`). Lua: only
  `base` (minus `dofile`, `loadfile`, `load` of binary chunks, `require`), `string`, `table`, `math`, `utf8`; no `io`,
  `os`, `package`, `debug`.
- **`pm` surface (v1)**: `pm.info`, `pm.variables`, `pm.environment`, `pm.collectionVariables`, `pm.globals`
  (`get`, `set`, `unset`, `has`, `clear`, `toObject`, plus `replaceIn` on `pm.variables`), `pm.request` (read in both
  phases, mutate in pre-request), `pm.response` (post-response only), `pm.test`, `pm.expect`, `pm.sendRequest`,
  `pm.cookies` (read-only, see below), `console.*`. Not supported and failing with
  `"pm.<name> is not supported in Yaapic"`: `pm.execution.setNextRequest` and `pm.iterationData` (Runner, milestone 6),
  `pm.vault`, `pm.visualizer`, `pm.require`, and the legacy `postman.*` globals except `postman.setEnvironmentVariable`,
  `postman.getEnvironmentVariable` and `postman.clearEnvironmentVariable`, which alias the environment calls.
- **Cookies.** There is no cookie jar (deferred by ADR 0008). `pm.cookies.get(name)`, `has(name)`, `toObject()` read the
  `Set-Cookie` headers of the current response (post-response only; empty in pre-request); `pm.cookies.jar()` throws
  `"The cookie jar is not available in Yaapic yet"`. The UI has no Cookies tab in M3.
- **Lua idioms.** The Lua `pm` mirrors the JavaScript one with the same names. Calls use dots (no colon) because the
  prelude binds closures: `pm.environment.set("a", "1")`, `pm.test("name", function() ... end)`,
  `pm.expect(x).to.equal(1)`. `pm.response.json()` returns a Lua table, `pm.response.text()` a string; JSON `null` maps
  to the sentinel `pm.null`. Chai property assertions (`.to.be.true`, `.to.be.ok`) become calls in Lua
  (`.to.be.is_true()`, `.to.be.ok()`): the Lua `expect` card lists the exact names.
- **Console and tests** are collected per run and returned in `ScriptRunResult` (Domain, M3-04); the UI shows them in the
  console panel and in the response "Tests" tab. Tests are not persisted in the history.
- **Native binaries.** QuickJS-NG and Lua 5.4 are built from pinned upstream source tags (SHA-256 checked) by a script
  in `native/` and copied next to the app (`runtimes/<rid>/native`); no binary is committed. The supported RIDs are the
  CI ones: `win-x64`, `linux-x64`, `osx-arm64`. If a library cannot be loaded at run time the engine reports
  "unavailable" and scripts of that language fail with a clear message while the rest of the app works (M3-47).
- **File format** (version stays 1, keys optional on read, always written, same order rules as ADR 0005): a request
  file gains `scripts` after `settings`; a folder file gains `scripts` after `variables`:

  ```yaml
  scripts:
    preRequest:
      language: javascript   # javascript | lua
      source: |
        pm.environment.set("ts", Date.now().toString());
    postResponse:
      language: javascript
      source: ""
  ```

- **Not in M3** (list them in the ADR): Python and C# scripting, `require` of npm modules, script debugging and
  breakpoints, a snippets sidebar, timers (`setTimeout`), collection-run integration, importing Postman scripts
  (milestone 5; they stay JavaScript).

## Docs to update

- New `docs/adr/0009-scripting.md` (Status `Accepted`; sections Context, Options, AOT risks, Decision, as ADR 0001 and
  0005) and one line in `docs/adr/README.md`.
- `docs/adr/0001-script-engines.md`: add a "See also" line pointing to ADR 0009 (decision unchanged).
- `docs/file-format.md`: add the `scripts` keys to the request and folder sections (the shape above) and to the key
  order lines. State that they are optional on read.

## Out of scope

Any code, the vault (ADR 0010, next card), UI.

## Acceptance criteria

- [x] ADR 0009 contains every decision above, with the AOT risks of the native bindings and the fallback when a native
      library is unavailable.
- [x] `docs/file-format.md` shows the `scripts` shape for request and folder files; ADR index updated.
- [x] No code changed; `dotnet build Yaapic.sln -c Release` still has 0 warnings.

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

`docs(adr): add ADR 0009 scripting architecture and file format`
