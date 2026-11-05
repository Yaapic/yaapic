# M3-14: `Yaapic.Scripting` project, host bridge and JSON protocol

Status: todo
Platform: any
Depends on: M3-07

## Goal

The new adapter project and the language-neutral half of an engine: the `__host(op, argsJson)` bridge that turns a JSON
call from a script into a call on `IScriptHost`. No native code yet.

## Design decisions

- Read first: ADR 0009, `docs/architecture.md`, `tests/Yaapic.Architecture.Tests/DependencyRulesTests.cs`,
  `src/Yaapic.Infrastructure/Yaapic.Infrastructure.csproj` (copy its settings).
- `src/Yaapic.Scripting/Yaapic.Scripting.csproj` (`net11.0`, references `Yaapic.Core` and `Yaapic.Domain`, AOT analyzers via
  `Directory.Build.props`, `AllowUnsafeBlocks` true for the later P/Invoke cards). Add it to `Yaapic.sln` and to
  `Yaapic.CrossPlatform.slnf`; add `["Yaapic.Scripting"] = ["Yaapic.Core", "Yaapic.Domain"]` to `DependencyRulesTests` and
  `"Yaapic.Scripting"` to the allowed references of `Yaapic.Composition`. New `tests/Yaapic.Scripting.Tests` (xunit v3, like
  `Yaapic.Infrastructure.Tests`, referencing `Yaapic.Scripting`, `Yaapic.Core`, `Yaapic.Domain`, `Yaapic.TestDoubles`), in both
  solution files. Update the table in `docs/architecture.md`.
- `internal static class HostOps` string constants (the contract of ADR 0009): `var.get`, `var.set`, `var.unset`, `var.has`,
  `var.clear`, `var.list`, `var.replace`, `req.get`, `req.set`, `res.get`, `info.get`, `console`, `test`, `send`, `cookies`,
  `unsupported`.
- DTO records with `[JsonSerializable]` source generation (`HostJsonContext : JsonSerializerContext`, camelCase,
  `UseStringEnumConverter` not needed): `VarArgs(string scope, string? name, string? value)`, `RequestDto`
  (`method, url, headers[{key,value}], bodyMode, bodyText, bodyContentType, urlEncoded[{key,value}]`), `ResponseDto`
  (`code, status, headers, text, size, time, url, cookies[...]`), `InfoDto`, `ConsoleArgs(level, text)`,
  `TestArgs(name, passed, message, ms)`, `SendResultDto(response?, error?)`, `CookieDto`. Scopes travel as the lowercase
  names `layered|local|environment|collection|globals`.
- `internal sealed class HostBridge(IScriptHost host, RunCollector collector)` with
  `string Invoke(string op, string argsJson)`: dispatches the op, returns `{"ok":...}` / `{"err":"..."}`, **never throws**
  (any exception, including `InvalidOperationException` from read-only requests and JSON errors, becomes `err` with the
  message). `req.set` sends the whole `RequestDto` back (the prelude keeps a local copy and flushes mutations: simpler than
  one op per property; document it). `unsupported` returns `err = "pm.<arg> is not supported in Yaapic"`.
- `internal sealed class RunCollector(ScriptLimits limits, TimeProvider time, string origin)` accumulates `ConsoleEntry`
  (truncating each message to `MaxConsoleEntryLength` with `…`, dropping beyond `MaxConsoleEntries` and adding one final
  `Warn` "Console output truncated" entry) and `TestResult` records; `Console`/`Tests` read-only snapshots.
- No script engine code here: the bridge is tested with a hand-written `IScriptHost` fake (reuse the M3-07 fake style).

## Out of scope

Native libraries (M3-15 to M3-17, M3-24, M3-25), preludes.

## Acceptance criteria

- [ ] Tests: every op round trips through `HostBridge` against a recording host (variables per scope, replace, request get/set
      incl. read-only failure -> `err`, response get with and without a response, info, console with truncation and entry
      cap, test record, send success and failure, cookies); malformed JSON and unknown op -> `err`, never an exception.
- [ ] Architecture tests green with the new project and rule; the solution and the cross-platform filter build with 0 warnings
      (AOT analyzers on); all tests green.

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

`feat(scripting): add the scripting project and the host bridge`
