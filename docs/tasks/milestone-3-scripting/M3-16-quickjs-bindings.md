# M3-16: QuickJS-NG P/Invoke bindings and AOT smoke

Status: done
Platform: any
Depends on: M3-15

## Goal

A thin, safe .NET layer over the QuickJS-NG C API: runtime and context handles, evaluation, error extraction, memory
limit, interrupt handler and one host callback. Proven under Native AOT. No `pm`, no `IScriptEngine` yet.

## Design decisions

- Read first: `quickjs.h` of the pinned tag (the extracted sources in `artifacts/native/_src`), ADR 0009 (AOT rules),
  ADR 0005 (AOT checks), M3-15 notes.
- `src/Yaapic.Scripting/QuickJs/`:
  - `internal static partial class QuickJsNative` with `[LibraryImport("quickjs-ng")]` declarations (blittable types only,
    `nint` for pointers, `[UnmanagedCallersOnly]` callbacks via function pointers `delegate* unmanaged[Cdecl]<...>`, no
    delegates marshalled). Library resolution: a `NativeLibrary.SetDllImportResolver` registered once from a module
    initializer-free static constructor (no reflection: use the assembly passed to the callback) that looks in
    `AppContext.BaseDirectory`, then `runtimes/<rid>/native`; missing library -> a `DllNotFoundException` caught by the
    engine card (M3-17) and reported as `Unavailable`.
  - **JSValue ABI**: QuickJS-NG's `JSValue` is a 16-byte struct on 64-bit (union + tag) unless NaN-boxing is on; mirror it
    as `[StructLayout(LayoutKind.Sequential)] readonly struct JSValue { nint/long u; long tag; }` after reading the pinned
    header and `JS_NAN_BOXING` setting, pass it by value (LibraryImport supports blittable structs). Record the layout
    decision in Notes. If a function needs a variadic or inline-only API (`JS_NewInt32` etc. are inline), re-implement the
    inline helpers in C# on the struct.
  - Safe wrappers: `sealed class QuickJsRuntime : IDisposable` (`JS_NewRuntime`, `JS_SetMemoryLimit`, `JS_SetMaxStackSize`,
    `JS_SetInterruptHandler` with an `[UnmanagedCallersOnly]` function that reads a `GCHandle`-held `InterruptState`
    (deadline ticks + cancellation flag) and returns 1 to abort), `sealed class QuickJsContext : IDisposable` (`JS_NewContext`
    or `JS_NewContextRaw` + only the intrinsics ADR 0009 allows: base objects, JSON, Date, Promise, RegExp, Proxy,
    Map/Set, typed arrays; **no** `std`/`os`, no eval of host modules), `Eval(string source, string fileName)` returning
    a `JsResult` (value converted to string/JSON, or `JsError(message, stack, line)` from `JS_GetException`),
    `DefineHostFunction(string name, HostCallback callback)` where `HostCallback` is `Func<string, string, string>` stored
    in a `GCHandle` referenced from the C function's `data` slot (`JS_NewCFunctionData`), `RunPendingJobs()` loop
    (`JS_ExecutePendingJob` until 0, error check), `JS_RunGC` on dispose, UTF-8 string marshalling helpers using
    `JS_ToCStringLen`/`JS_FreeCString`, and **every** `JSValue` freed (`JS_FreeValue`); a debug-only leak counter in tests.
  - Out-of-memory and interrupt map to `JsError.Kind` (`Exception`, `Interrupted`, `OutOfMemory`).
- `tools/NativeSmoke/` (new console project, `PublishAot` true, not packable, added to `Yaapic.sln` and
  `Yaapic.CrossPlatform.slnf`): loads the binding, evaluates `1+1`, a function calling a host callback, an infinite loop with
  a 200 ms interrupt, and a 1 MB allocation loop with a 4 MB memory limit; prints `OK` and exits 0. The AOT publish of this
  project must show **0 IL2xxx/IL3xxx**; run the published exe and check its exit code. CI runs the same publish and run
  (add a step after "Publish with Native AOT": `dotnet publish tools/NativeSmoke ... && run`, on all three OSes).

## Out of scope

`IScriptEngine`, `pm`, `console`, timers, modules, bytecode loading (`JS_ReadObject`), Atomics/SharedArrayBuffer.

## Acceptance criteria

- [x] Tests (`Yaapic.Scripting.Tests`): eval of numbers, strings, JSON, unicode; syntax error and thrown error carry message
      and line; a host callback receives and returns strings (including unicode and 1 MB payloads); an infinite loop is
      interrupted by the deadline and by a cancellation flag; the memory limit stops a runaway allocation with
      `OutOfMemory`; `Promise.resolve().then(...)` runs through `RunPendingJobs`; context and runtime dispose repeatedly
      without crash; no leaked `JSValue` (counter == 0) after all of the above; `std`, `os` and `require` are `undefined`.
- [x] `tools/NativeSmoke` AOT publish: 0 IL2xxx/IL3xxx; the published exe prints `OK`, exit code 0 (host RID).
- [x] 0 warnings, XML docs on public members (types may be `internal`), all tests green.
- [x] Notes record the `JSValue` layout, the intrinsics list and any API that had to be re-implemented.

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

`feat(scripting): add QuickJS-NG bindings and AOT smoke test`

## Notes

- `JSValue` layout: NaN boxing is off on 64-bit (`JS_NAN_BOXING` only defaults on for 32-bit), so `JSValue` is
  `struct { union u (8 bytes); int64 tag; }` = 16 bytes, mirrored as `JSValue(ulong Bits, long Tag)` and passed by value
  through `LibraryImport`. 32-bit processes are refused with `PlatformNotSupportedException`.
- Intrinsics (on `JS_NewContextRaw`): base objects, Date, Eval, RegExp (+ compiler), JSON, Proxy, Map/Set, typed arrays,
  Promise. No `std`, `os`, module loader, BigInt, WeakRef or DOMException: `std`, `os` and `require` are `undefined`.
- Re-implemented in C#: `JS_IsException`/`JS_IsUndefined` and the `JS_UNDEFINED`/`JS_EXCEPTION` constants (inline in the
  header), `JS_ToCStringLen` (calls `JS_ToCStringLen2`), the host-function error throw (`JS_NewError` + `message`
  property + `JS_Throw`, since `JS_ThrowInternalError` is variadic).
- Host callback: `JS_NewCFunctionData` with the callback index in `magic`; the context is found again from
  `JS_GetContextOpaque` (a `GCHandle` to the `QuickJsContext`). The JS function passes its first two arguments as strings.
- Interrupt: `InterruptState` (deadline via `Stopwatch` timestamp, cancellation flag, `Fired`); a failure while `Fired` is
  `Interrupted`, a message containing `out of memory` is `OutOfMemory`. The default JS stack limit is 256 KiB so it stays
  under the 1 MB secondary-thread stack on Windows.
- A rejected promise is not a failed job: `RunPendingJobs` only returns an error when `JS_ExecutePendingJob` fails.
- Library resolution: `NativeLibraryResolver` (shared with the Lua bindings of M3-24) tries `AppContext.BaseDirectory`, then
  `runtimes/<rid>/native`.
- `tools/NativeSmoke` published with Native AOT for win-x64: 0 IL2xxx/IL3xxx, exe prints `OK`, exit code 0. The CI step was
  added but not run; only win-x64 was verified locally.
- `dotnet test` through `rtk` showed handshake failures with exit code 5 for every project while `rtk bypass` passed:
  the condensed output was unusable for that command.
