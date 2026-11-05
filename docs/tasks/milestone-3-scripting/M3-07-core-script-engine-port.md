# M3-07: Core: script engine port, host contract and registry

Status: done
Platform: any
Depends on: M3-03, M3-04

## Goal

The port every engine implements and the host contract every engine calls, plus a registry and a fake engine for the
tests of the layers above. No native code yet.

## Design decisions

- Read first: `docs/adr/0009-scripting.md`. Namespace `Yaapic.Core.Scripting`, folder `src/Yaapic.Core/Scripting/`.
- `public interface IScriptEngine`: `ScriptLanguage Language { get; }`; `bool IsAvailable(out string? reason)`;
  `Task<ScriptRunResult> RunAsync(ScriptInvocation invocation, CancellationToken ct)`. Contract (XML docs): never throws
  for script problems (they become `ScriptRunResult` statuses), throws only `ArgumentNullException`; honours the limits
  and the token; the returned console and test lists already respect `ScriptLimits`.
- `public sealed record ScriptInvocation(ScriptBlock Block, ScriptPhase Phase, string Origin, IScriptHost Host, ScriptLimits Limits)`.
- `public sealed record ScriptLimits(TimeSpan Timeout, long MemoryLimitBytes, int MaxSendRequests, int MaxConsoleEntries,
  int MaxConsoleEntryLength)` with `static ScriptLimits Default` = 5 s, 64 MiB, 20, 1000, 8192 (ADR 0009).
- `public enum ScriptScope { Layered, Local, Environment, Collection, Globals }` (`Layered` = the `pm.variables` view).
- `public interface IScriptHost` (implemented by M3-08 to M3-13, faked in tests):
  - `ScriptInfo Info { get; }` where `public sealed record ScriptInfo(string EventName, string RequestName, Guid RequestId, string Origin)`;
    `EventName` is `"prerequest"` or `"test"` (Postman names).
  - `ScriptRequest Request { get; }` and `ScriptResponse? Response { get; }` (types arrive in M3-09; declare them there:
    this card declares the two members and a minimal placeholder file is **not** allowed, so M3-09 is a dependency of the
    interface's final shape. Resolve this by creating `ScriptRequest` and `ScriptResponse` as empty `sealed class` /
    `sealed record` stubs here with a `// filled by M3-09` comment, and keeping them `public` so the interface compiles).
  - `bool TryGetVariable(ScriptScope scope, string name, out string value)`, `void SetVariable(ScriptScope scope, string name, string value)`,
    `bool UnsetVariable(ScriptScope scope, string name)`, `void ClearVariables(ScriptScope scope)`,
    `IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope)` (enabled rows; `Layered` merges, highest layer wins),
    `string ReplaceIn(string template)`.
  - `ScriptSendResult Send(ScriptRequest request)` (blocking: engines run on their own thread) with
    `public sealed record ScriptSendResult(ScriptResponse? Response, string? Error)`; `Error` is the failure text
    (timeout, DNS, invalid URL, send limit reached).
  - `IReadOnlyList<ScriptCookie> ResponseCookies { get; }` (`ScriptCookie` stub here too, filled by M3-09; empty in pre-request).
- `public sealed class ScriptEngineRegistry`: `Add(IScriptEngine)` (one per language; a second add for the same language
  replaces the first), `IScriptEngine Get(ScriptLanguage language)` returning the registered engine or an internal
  `UnavailableScriptEngine` whose `RunAsync` returns `ScriptRunResult.Unavailable(phase, origin, language, "No <language> engine is installed")`.
  `bool IsAvailable(ScriptLanguage, out string? reason)`.
- Test double in `tests/Yaapic.TestDoubles/FakeScriptEngine.cs`: constructor takes a `Func<ScriptInvocation, ScriptRunResult>`
  (or a queue of results) and records the invocations; `Language` settable; supports an optional delay honouring the token.

## Out of scope

Real engines, host implementations, the pipeline, JSON bridging.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: registry returns the registered engine, replaces on a second add, returns the
      unavailable engine for a missing language with `Status == Unavailable` and the reason; `ScriptLimits.Default` values;
      `FakeScriptEngine` records invocations and honours cancellation.
- [x] Architecture tests green (Core still references only Domain), 0 warnings, XML docs complete, all tests green.

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

`feat(core): add script engine port, host contract and registry`
