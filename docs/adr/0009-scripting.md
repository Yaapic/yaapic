# ADR 0009: Scripting architecture, host protocol and file format

Status: Accepted

See also: [ADR 0001](0001-script-engines.md) (engine choice), [ADR 0005](0005-storage-format.md),
[ADR 0008](0008-collections-globals-history-storage.md). The secret vault is [ADR 0010](0010-secret-vault.md).

## Context

Milestone 3 adds pre-request and post-response scripts in JavaScript and Lua with a Postman-style `yaapic` API, a console,
test results and limits. Every scripting card reads this ADR first. It fixes the port and adapters, the host protocol,
the semantics of a scripted send, the supported `yaapic` surface, the sandbox, the native binaries and the file format.

## Options

1. **Typed native bindings per `yaapic` function.** One P/Invoke callback per operation. Many entry points per engine, each
   needing its own marshalling and tests.
2. **One native function plus a script-language prelude.** Scripts call `__host(op, argsJson) -> resultJson`; `yaapic`,
   `console` and `expect` are written in the script language on top of it. One bridge to bind and test per engine.
3. **Managed JavaScript engine or Roslyn/IronPython.** Rejected by ADR 0001 (reflection and code generation).

## AOT risks

- Both engines are native libraries bound with source-generated `LibraryImport` P/Invokes only: no reflection, no
  `DynamicDependency`, no runtime code generation, no `Marshal.GetDelegateForFunctionPointer`. Callbacks use
  `UnmanagedCallersOnly` function pointers.
- The bridge decodes `argsJson` and encodes results with `System.Text.Json` source generators (`HostJsonContext`); no
  reflection-based serialisation.
- Native binaries must exist for every supported RID and be kept current. They are built from pinned upstream source
  tags (SHA-256 checked) by a script in `native/` and copied next to the app (`runtimes/<rid>/native`); no binary is
  committed. Supported RIDs are the CI ones: `win-x64`, `linux-x64`, `osx-arm64`.
- **Fallback when a library cannot be loaded** (missing file, unsupported RID, load failure): the engine reports
  "unavailable"; scripts of that language fail with a clear message naming the language and the reason, and the rest of
  the app (sending, editing, other language) keeps working (M3-47).
- Preludes are C# `const string` per engine, so nothing is read from disk or generated at run time.

## Decision

### Port and adapters

`IScriptEngine` and everything an engine needs from the application (`IScriptHost`, `ScriptInvocation`,
`ScriptLimits`) live in `Yaapic.Core.Scripting` (ADR 0001: port in `Yaapic.Core`). The adapters live in the new project
`Yaapic.Scripting`, which references `Yaapic.Core` and `Yaapic.Domain` only (rule added to `DependencyRulesTests` by
M3-14). `Yaapic.Composition` references it; ViewModels never do.

### Engines and host protocol

QuickJS-NG (JavaScript) and Lua 5.4. One native function is exposed to scripts, `__host(op, argsJson) -> resultJson`.
Everything else (`yaapic`, `console`, `expect`) is a prelude in the script language. The bridge decodes `argsJson` with
`HostJsonContext` and calls the typed `IScriptHost`. The result is `{"ok":<json>}` or `{"err":"<message>"}`; the
prelude throws on `err`. Op names are constants in `HostOps` (M3-14).

### Scripts per owner

A request and a folder (a top-level folder is the collection) each own a `ScriptSet` with one pre-request block and one
post-response block. Each block has its own language (`javascript` or `lua`, default `javascript`) and source text, so a
collection can use JavaScript while one request uses Lua.

### Execution order (Postman order)

Pre-request: collection, then each folder from the outermost to the innermost, then the request. Post-response: the same
order (collection first). An empty block (blank source) is skipped.

### What scripts see

Pre-request scripts see the request definition as written (URL, headers, body keep their `{{variables}}`);
`yaapic.variables.replaceIn(text)` resolves a string. The sender resolves the (possibly mutated) definition afterwards, so a
variable set by a pre-request script is visible to `{{name}}` in the same send. Post-response scripts additionally see
the response (read-only) and the request as it was sent.

### Variable layers

Highest first: local (`yaapic.variables.set`, lives for one send), environment, collection, globals, dynamic (read-only).
`yaapic.environment.set`, `yaapic.collectionVariables.set` and `yaapic.globals.set` write to the layered overlay at once and are
persisted after the whole chain ran, through the existing stores (M3-44). When the scope does not exist (no active
environment, request outside a collection) the value lives in the local layer for that send and a `warn` console line
says so.

### Error policy

A script error (exception, timeout, memory limit) in a pre-request block aborts the send: the response panel shows the
error with origin, line and message, nothing is sent, and the variable changes made before the error are discarded. An
error in a post-response block never discards the response: it is shown next to the response and the tests of that block
already recorded are kept. A failed `yaapic.test` is a test result, not a script error.

### Limits

`ScriptLimits` (one record, defaults): 5 s wall-clock per block, 64 MiB memory per run, 20 `yaapic.sendRequest` calls per
run, 1 000 console entries per send, 8 192 characters per console entry (truncated with an ellipsis). Cancelling the send
cancels the script.

### Sandbox

No file system, no `require`/`import` of host modules, no `process`, no timers, no network except `yaapic.sendRequest`
(host-mediated, uses the app transport). JavaScript: QuickJS core only (no `std`/`os`). Lua: only `base` (minus
`dofile`, `loadfile`, `load` of binary chunks, `require`), `string`, `table`, `math`, `utf8`; no `io`, `os`, `package`,
`debug`.

### `yaapic` surface (v1)

`yaapic.info`, `yaapic.variables`, `yaapic.environment`, `yaapic.collectionVariables`, `yaapic.globals` (`get`, `set`, `unset`, `has`,
`clear`, `toObject`, plus `replaceIn` on `yaapic.variables`), `yaapic.request` (read in both phases, mutate in pre-request),
`yaapic.response` (post-response only), `yaapic.test`, `yaapic.expect`, `yaapic.sendRequest`, `yaapic.cookies` (read-only), `console.*`.

Not supported, failing with `"yaapic.<name> is not supported in Yaapic"`: `yaapic.execution.setNextRequest` and
`yaapic.iterationData` (Runner, milestone 6), `yaapic.vault`, `yaapic.visualizer`, `yaapic.require`, and the legacy `postman.*` globals
except `postman.setEnvironmentVariable`, `postman.getEnvironmentVariable` and `postman.clearEnvironmentVariable`, which
alias the environment calls.

### Cookies

There is no cookie jar (deferred by ADR 0008). `yaapic.cookies.get(name)`, `has(name)` and `toObject()` read the
`Set-Cookie` headers of the current response (post-response only; empty in pre-request). `yaapic.cookies.jar()` throws
`"The cookie jar is not available in Yaapic yet"`. The UI has no Cookies tab in M3.

### Lua idioms

The Lua `yaapic` mirrors the JavaScript one with the same names. Calls use dots (no colon) because the prelude binds
closures: `yaapic.environment.set("a", "1")`, `yaapic.test("name", function() ... end)`, `yaapic.expect(x).to.equal(1)`.
`yaapic.response.json()` returns a Lua table, `yaapic.response.text()` a string; JSON `null` maps to the sentinel `yaapic.null`.
Chai property assertions (`.to.be.true`, `.to.be.ok`) become calls in Lua (`.to.be.is_true()`, `.to.be.ok()`); the Lua
`expect` card lists the exact names.

### Console and tests

Collected per run and returned in `ScriptRunResult` (Domain, M3-04). The UI shows them in the console panel and in the
response "Tests" tab. Tests are not persisted in the history.

### File format

Version stays 1, keys optional on read, always written, same order rules as ADR 0005. A request file gains `scripts`
after `settings`; a folder file gains `scripts` after `variables`:

```yaml
scripts:
  preRequest:
    language: javascript   # javascript | lua
    source: |
      yaapic.environment.set("ts", Date.now().toString());
  postResponse:
    language: javascript
    source: ""
```

### Not in M3

Python and C# scripting, `require` of npm modules, script debugging and breakpoints, a snippets sidebar, timers
(`setTimeout`), collection-run integration, importing Postman scripts (milestone 5; they stay JavaScript).

See [scripting-compatibility.md](../scripting-compatibility.md) for the supported `yaapic` surface and the differences from Postman.

## Implementation notes (M3)

Deviations and details found while building milestone 3 (read from the notes of the M3 cards and checked against the code):

- **Host operations.** `HostOps` has `var.get`, `var.set`, `var.unset`, `var.has`, `var.clear`, `var.list`, `var.replace`, `req.get`,
  `req.set`, `res.get`, `info.get`, `console`, `console.plain`, `test`, `send`, `cookies` and `unsupported`. Every
  `yaapic.<name> is not supported in Yaapic` error goes through `unsupported`.
- **Native libraries.** They are copied next to the executable by an MSBuild target of `Yaapic.Scripting` (not into
  `runtimes/<rid>/native` as first written); `NativeLibraryResolver` tries the application directory, then
  `runtimes/<rid>/native`. The target runs `pwsh native/Build-Natives.ps1` when `artifacts/native/<rid>` is missing. Only
  `win-x64` was built and run by hand; Linux and macOS are built by CI.
- **QuickJS.** Intrinsics: base objects, `Date`, `eval`, `RegExp`, `JSON`, `Proxy`, `Map`/`Set`, typed arrays, `Promise`. No
  `std`, `os`, module loader, `BigInt`, `WeakRef` or `DOMException`. 64-bit only (32-bit processes are refused). The JavaScript
  stack limit is 256 KiB so that it fits the 1 MB stack of a secondary thread on Windows.
- **Lua shim.** A Lua error is a `longjmp` and must not cross a managed frame under Native AOT, so `native/lua/yaapic_lua.c`
  exposes a non-raising `yl_*` API (see [dependencies.md](../dependencies.md)); it owns the memory limit, the deadline and cancel
  hook and the traceback. `print` and `warn` are removed in addition to the functions listed above, `collectgarbage` only
  accepts `"count"`, chunks load in text mode only, and out of memory is reported even when a script catches the error with
  `pcall`. After a timeout or a cancel the hook fires at every instruction, so `pcall` cannot swallow it.
- **Sandbox surface.** The `yaapic.info` object has `iteration: 0` and `iterationCount: 1`; `yaapic.environment.name` is not supported.
- **Redaction.** Console entries, test results and script errors are redacted by `ScriptedRequestSender` (ADR 0010).
- **Persisting variable changes.** `ApplyChangesAsync` silently skips changes whose name is not a creatable variable name, and
  saving an environment or the globals also saves the unsaved edits of the same scope held by an open dialog.
- **Script template.** The editors show a template for an empty block; a block whose source equals the template (or is blank)
  is empty and the template is never persisted.
- **Lua tests.** Test durations are recorded as 0 ms in Lua.
- **WinUI editor.** Ctrl+Enter inside the script editor does not send (the editor consumes it); undo history is not kept across
  the pre-request and post-response phases.
- **Lua highlighting.** LoomEditor 0.5.0 had no Lua grammar, so Lua was plain text in the editors at first; 0.6.0 ships it and
  both editors highlight Lua.
- **Not verified on every OS.** The Linux and macOS native builds and the Avalonia runtime checks were not run by hand (see the
  Notes of M3-15, M3-49 and M3-63).
