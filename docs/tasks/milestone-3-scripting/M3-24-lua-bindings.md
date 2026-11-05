# M3-24: Lua 5.4 native shim, bindings and AOT smoke

Status: todo
Platform: any
Depends on: M3-15

## Goal

A safe .NET layer over Lua 5.4: a state with a memory limit, a restricted standard library, deadline and cancellation, one
host function, and error extraction. Proven under Native AOT. No `pm`, no `IScriptEngine` yet.

## Design decisions

- Read first: the Lua 5.4 reference manual (error handling, `lua_pcall`, `lua_sethook`, allocator), the Lua sources pinned
  by M3-15, ADR 0009 (sandbox list, limits), M3-16 (style of the managed layer, AOT smoke tool).
- **The longjmp rule (read twice).** Lua raises errors with `longjmp`; a `longjmp` that crosses a managed (.NET) frame is
  undefined behaviour and crashes under Native AOT. Therefore **managed code never calls an API function that can raise
  outside a protected call, and no managed function is ever on the C stack between a Lua error and its `lua_pcall`.**
  The solution is a small C shim compiled into the Lua library: `native/lua/yaapic_lua.c` (add it to
  `native/lua/CMakeLists.txt`, export with the same macro as the Lua API; update `native/Build-Natives.ps1` only if needed)
  exposing **non-raising** functions (each runs its Lua work inside `lua_pcall`/a protected `lua_cpcall`-style helper and
  returns an int status):
  - `yl_new(long long memory_limit)` -> `lua_State*` (custom allocator implemented in C that enforces the limit and returns
    NULL beyond it, so Lua raises its own out-of-memory error), `yl_close(L)`.
  - `yl_open_libs(L)`: `luaopen_base` (then remove `dofile`, `loadfile`, `load`, `loadstring`, `require`, `collectgarbage`
    keeps only `"count"`), `string`, `table`, `math`, `utf8`, `coroutine`; **not** `io`, `os`, `package`, `debug`;
    `string.dump` removed; `math.random` stays (seeded by the host in M3-25).
  - `yl_set_host(L, host_cb)`: registers the global function `__host(op, argsJson)`; the C function calls
    `host_cb(const char* op, const char* args, char** result, int* resultLen)` (managed callback, never raises: returns 0 or
    non-zero), pushes the returned string (freed through `yl_free`) and, on non-zero, calls `luaL_error` **from C**.
  - `yl_set_limits(L, deadline_ticks_ptr, cancel_flag_ptr)`: installs a count hook (every 1000 instructions) that reads a
    managed-owned `volatile long long` deadline and a cancel flag and raises `luaL_error("script timed out")` or
    `"script canceled"` from C; the shim records which of the two fired in a field read by `yl_last_abort(L)`.
  - `yl_run(L, const char* chunk, size_t len, const char* chunk_name, char** errMsg, int* errLine)` -> status (0 ok, 1 runtime
    error, 2 syntax error, 3 out of memory, 4 timeout, 5 canceled); builds the traceback message in C with `luaL_traceback`
    under an error handler and returns it through `errMsg` (caller frees with `yl_free`).
  - `yl_set_registry_string / get` helpers if the engine needs them; `yl_free(void*)`.
- `src/Yaapic.Scripting/Lua/`: `LuaNative` (`[LibraryImport("lua54")]` for the `yl_*` functions only; host callback as a
  function pointer to an `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]` method that catches **every**
  exception and returns non-zero with the message), `LuaState : IDisposable` wrapper with `Run(string source, string name)`
  returning `LuaResult` (`Ok`, or `LuaError(Kind, Message, Line)`), `DefineHost(Func<string, string, string>)`, deadline and
  cancel plumbing (pinned `long` fields). Same native-library resolver as M3-16 (share the resolver class).
- Extend `tools/NativeSmoke` (M3-16) with a Lua part: `1+1`, a host callback, `while true do end` with a 200 ms deadline,
  `string.rep("x", 1e8)` with a 4 MB limit; prints `OK`.

## Out of scope

`IScriptEngine`, `pm`, coroutines used by scripts beyond what the base library allows, binary chunk loading.

## Acceptance criteria

- [ ] Tests (`Yaapic.Scripting.Tests`): syntax error and runtime error carry message and line; `error("x")` with a table;
      host callback round trip including unicode and a 1 MB string; a host callback that throws makes the Lua call raise
      (catchable with `pcall` inside the script) and never crashes; infinite loop -> timeout status; cancel flag -> canceled;
      memory limit -> out of memory status; `io`, `os`, `package`, `debug`, `dofile`, `loadfile`, `require`, `load` with a
      binary chunk, `string.dump` are `nil`/refused; `print` is `nil` (M3-25 defines console); 1 000 sequential states without
      leak growth; two states on two threads concurrently.
- [ ] `tools/NativeSmoke` AOT publish 0 IL2xxx/IL3xxx and prints `OK` on the host RID; CI step from M3-16 covers it.
- [ ] `native/Build-Natives.ps1` rebuilds the library with the shim from a clean cache; `docs/dependencies.md` notes the shim.
- [ ] 0 warnings, XML docs, all tests green. Notes record the shim API as built.

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

`feat(scripting): add the Lua shim and bindings with AOT smoke`
