# M3-25: `LuaEngine`: run a block with console, limits and the host bridge

Status: done
Platform: any
Depends on: M3-24, M3-17

## Goal

`LuaEngine : IScriptEngine`, the Lua twin of `QuickJsEngine`: dedicated thread, fresh state per run, limits, console
capture and the `__host` bridge shared with JavaScript.

## Design decisions

- Read first: M3-17 (structure, result mapping, `RunCollector`, `HostBridge`), M3-24.
- `src/Yaapic.Scripting/Lua/LuaEngine.cs`: `public sealed class LuaEngine : IScriptEngine` (`Language` = `Lua`),
  `IsAvailable` as in M3-17 (library resolution + `yl_new` smoke, cached). `RunAsync`: dedicated thread `yaapic-lua`
  (1 MiB stack... Lua runs in C stack too: use 2 MiB and set `LUAI_MAXCCALLS` default), fresh `LuaState` per run with
  `limits.MemoryLimitBytes`, deadline from `limits.Timeout`, cancel flag from the token, `yl_open_libs`, host bridge
  (`HostBridge.Invoke`), the **Lua prelude core** (`LuaPrelude.Core`, `const string`, evaluated as its own chunk) defining:
  - `console.log/info/warn/error/debug(...)` and `print` (alias of `console.log`): arguments formatted with `tostring`,
    tables printed as `{ a = 1, b = { 2, 3 } }` (keys sorted, depth 3, `...` beyond, cycles `<cycle>`), joined with a
    space, sent through `__host("console", ...)`. Because JSON encoding arrives in M3-26, this card builds the argument
    string for `console` as `level .. "\0" .. text` handled by a dedicated tiny op variant: add `HostOps.ConsolePlain`
    (`"console.plain"`, args = `level\ntext`) to the bridge (M3-14 class) so no JSON is needed yet.
  - `__yaapic = { phase, origin }`, `math.randomseed(os-free seed from the host: __host("info.get"))` is **not** needed: seed
    `math.random` with the run's start ticks passed via a `yl` registry string.
- Result mapping as M3-17: `Failed` (message `<origin>:<line>: <message>`, `ScriptError.Line` extracted), `TimedOut`,
  `Canceled`, `MemoryLimit`, `Unavailable`. Line numbers of the user chunk are exact (the prelude is a separate chunk).
  Chunk name = `=<origin>` so tracebacks read `pre-request · Users API:3:`.
- Sandbox as ADR 0009: no `io`/`os`/`package`/`debug`; `require`, `dofile`, `loadfile` are `nil`; `load` only accepts text
  (mode `"t"`) and still runs under the same limits.

## Out of scope

`pm` (M3-26 onward), JSON codec, `pm.sendRequest`.

## Acceptance criteria

- [x] Tests (real Lua, fake host): `console.*` and `print` levels and formatting (nested tables, booleans, nil, cycles, very
      long strings cut by the collector), 10 000 entries capped, thrown error and `error({code=1})`, syntax error with line,
      timeout (300 ms limit), cancellation, memory limit, `io`/`os`/`require` are `nil`, two runs share nothing, concurrent
      runs, `IsAvailable` false with a reason when the library is missing, `__host` round trip with an `err` result
      raising a Lua error catchable by `pcall`.
- [x] `tools/NativeSmoke` runs one `LuaEngine` block; AOT publish 0 IL2xxx/IL3xxx, `OK`.
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

`feat(scripting): add the Lua engine with limits and console capture`
