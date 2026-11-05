# M3-04: Domain: console entries, test results and script run result

Status: done
Platform: any
Depends on: M3-03

## Goal

The immutable result types every layer shares: what a script run logged, which tests passed and how the run ended.

## Design decisions

- Namespace `Yaapic.Domain.Scripting`.
- `public enum ConsoleLevel { Log, Info, Warn, Error, Debug }`.
- `public enum ConsoleSource { Script, Network, Test, System }` (`System` = lines written by Yaapic itself, for example
  "pre-request · Collection" headers or the variable-scope warning).
- `public sealed record ConsoleEntry(DateTimeOffset At, ConsoleLevel Level, ConsoleSource Source, string Message, string? Origin = null)`:
  `Origin` is a free text such as `"pre-request · Users API"`; `Message` has no length rule here (the engine truncates).
- `public sealed record TestResult(string Name, bool Passed, string? Message, TimeSpan Duration, string Origin)`:
  `Message` is the assertion message of a failed test, null when it passed.
- `public sealed record ScriptError(string Message, int? Line = null, int? Column = null, string? Stack = null)`.
- `public enum ScriptRunStatus { Succeeded, Failed, TimedOut, MemoryLimit, Canceled, Unavailable }` (`Unavailable` = the
  engine for the language could not be loaded).
- `public sealed record ScriptRunResult(ScriptPhase Phase, string Origin, ScriptLanguage Language, ScriptRunStatus Status,
  ScriptError? Error, IReadOnlyList<ConsoleEntry> Console, IReadOnlyList<TestResult> Tests, TimeSpan Duration)`:
  `bool Succeeded => Status == Succeeded`; static factories `Skipped(phase, origin, language)` (Succeeded, no output, zero
  duration) and `Unavailable(phase, origin, language, string reason)` (Status `Unavailable`, `Error` = reason).
- `public static class TestSummary` with `(int Passed, int Failed) Count(IEnumerable<TestResult>)` and
  `string Format(IEnumerable<TestResult>)` returning `"6/6"` (passed/total) or `"4/6"`, empty text for no tests (the UI
  badge text of the response "Tests" tab).

## Out of scope

Producing these values (engines, M3-17), persistence (not stored), UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Domain.Tests`: both factories; `Succeeded`; `TestSummary.Count/Format` for none, all passed, mixed.
- [x] 0 warnings, XML docs on every public member, all tests green.

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

`feat(domain): add console, test result and script run result types`
