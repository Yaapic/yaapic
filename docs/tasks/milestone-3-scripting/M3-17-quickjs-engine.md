# M3-17: `QuickJsEngine`: run a block with console, timeout and the host bridge

Status: done
Platform: any
Depends on: M3-16, M3-14

## Goal

`QuickJsEngine : IScriptEngine`: runs one JavaScript block against an `IScriptHost` with limits, console capture, the
`__host` function, a minimal `pm` placeholder-free setup (the `pm` prelude comes in M3-18). After this card a script can call
`console.log` and `__host`, time out, run out of memory and be cancelled.

## Design decisions

- Read first: M3-14 (`HostBridge`, `RunCollector`), M3-16, ADR 0009 (limits, sandbox, error policy).
- `src/Yaapic.Scripting/QuickJs/QuickJsEngine.cs`: `public sealed class QuickJsEngine : IScriptEngine` (`Language` =
  `JavaScript`). `IsAvailable` tries to initialise the native binding once (cached; a failure keeps the exception message
  as `reason`, for example "quickjs-ng library not found in <dir>"). `RunAsync`:
  1. Runs on a **dedicated thread** (`new Thread` with a `TaskCompletionSource<ScriptRunResult>`, name `yaapic-js`,
     background, stack size 1 MiB; never the thread pool: host calls block) so `IScriptHost.Send` can block.
  2. Creates a fresh `QuickJsRuntime` + `QuickJsContext` per run (no shared state between runs), sets the memory limit and
     the stack limit (1 MiB), the interrupt handler with `deadline = now + limits.Timeout` and the cancellation token.
  3. Defines `__host` (bridge, M3-14), evaluates the **engine prelude** (`JsPrelude.Core`, a `const string` in
     `QuickJsPrelude.cs`; this card only contains the `console` object: `console.log/info/warn/error/debug` formatting
     arguments with a small `format()` (strings as is, numbers/booleans via `String`, objects/arrays via `JSON.stringify`
     with a circular-safe fallback `[Circular]`, `undefined` -> `undefined`, `Error` -> `message` plus `stack` first line)
     and calling `__host("console", {level, text})`; plus a frozen `__yaapic` object with the phase and origin). The `pm`
     prelude is appended by M3-18 to M3-22 as further `const string`s concatenated in a fixed order by a single
     `JsPrelude.Build()`.
  4. Evaluates the user source (`fileName` = the `Origin`, so stack traces read `at pre-request · Users API:3`), runs
     pending jobs (so a script using `await`/promises completes; a script that leaves a promise pending forever is cut by
     the timeout), then builds the `ScriptRunResult`.
- Result mapping: normal end -> `Succeeded`; exception -> `Failed` with `ScriptError(message, line, column, stack)` (line
  adjusted when the prelude shifts it: the prelude is evaluated as a separate script, never concatenated with the user
  source, so user line numbers are exact); interrupt by deadline -> `TimedOut` ("Script timed out after 5 s"); by token ->
  `Canceled`; out of memory -> `MemoryLimit`; native load failure -> `Unavailable`. Console and tests come from the
  `RunCollector`. `Duration` measured with `TimeProvider`.
- The user source runs as a **classic script** in the global scope but wrapped so top-level `return`... is **not** wrapped
  (Postman scripts do not use top-level return); top-level `await` is supported by evaluating with `JS_EVAL_FLAG_ASYNC`
  when the pinned header offers it, otherwise wrap in an async IIFE and note the line offset.
- No globals leak between runs; `globalThis` has no `process`, `require`, `module`, `fetch`, `setTimeout`.

## Out of scope

The `pm` object (M3-18 onward), `pm.sendRequest` callbacks (M3-22), Lua, wiring into the app (M3-47).

## Acceptance criteria

- [x] Tests (`Yaapic.Scripting.Tests`, real QuickJS, fake host): `console.*` entries with levels, formatting of objects,
      arrays, errors, circular objects, 10 000 entries capped at 1 000 with the truncation warning, a 20 000 character
      message truncated; a thrown error reports message, exact user line and column and `Failed`; syntax error is `Failed`;
      `while(true){}` is `TimedOut` within timeout + 500 ms (use a 300 ms limit in the test); cancellation token ->
      `Canceled` promptly; runaway allocation -> `MemoryLimit`; `await` of a resolved promise works, a never-resolved one
      times out; `process`, `require`, `setTimeout`, `fetch` are `undefined`; two sequential runs share nothing; `__host`
      calls reach the fake host and an `err` result throws in the script; two engines running concurrently on different
      threads do not interfere; `IsAvailable` false with the reason when the library cannot be loaded (simulate through
      an internal resolver override).
- [x] Native AOT: extend `tools/NativeSmoke` to run one `QuickJsEngine` block with a fake host; AOT publish 0 IL2xxx/IL3xxx,
      published exe prints `OK`.
- [x] 0 warnings, XML docs, all tests green.

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

`feat(scripting): add the QuickJS engine with limits and console capture`
