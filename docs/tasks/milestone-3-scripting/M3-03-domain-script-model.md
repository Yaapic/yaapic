# M3-03: Domain: script model on requests and folders

Status: done
Platform: any
Depends on: M3-01

## Goal

The domain types that describe scripts, and the `Scripts` member of `HttpRequestDefinition` and `FolderDefinition`.

## Design decisions

- Read first: `docs/adr/0009-scripting.md`, `src/Yaapic.Domain/Http/HttpRequestDefinition.cs`,
  `src/Yaapic.Domain/Collections/FolderDefinition.cs`. New namespace `Yaapic.Domain.Scripting`, folder
  `src/Yaapic.Domain/Scripting/`.
- `public enum ScriptLanguage { JavaScript, Lua }` with extension `ScriptLanguageExtensions`: `ToText()` returns
  `"javascript"` / `"lua"`, `TryParse(string? text, out ScriptLanguage)` (case-insensitive; unknown -> false).
- `public enum ScriptPhase { PreRequest, PostResponse }`.
- `public sealed record ScriptBlock(ScriptLanguage Language, string Source)`: `static ScriptBlock Empty` (JavaScript,
  `""`), `bool IsEmpty` (`string.IsNullOrWhiteSpace(Source)`).
- `public sealed record ScriptSet(ScriptBlock PreRequest, ScriptBlock PostResponse)`: `static ScriptSet Empty`, `bool IsEmpty`
  (both blocks empty), `ScriptBlock Get(ScriptPhase phase)`, `ScriptSet With(ScriptPhase phase, ScriptBlock block)`.
- `HttpRequestDefinition` and `FolderDefinition` gain a last positional parameter `ScriptSet Scripts` **with the default
  `ScriptSet.Empty`** (use a default parameter value so existing call sites keep compiling; do not touch the call sites).
  `Create(...)` factories pass `ScriptSet.Empty`. `IsEquivalentTo` compares `Scripts` (records compare by value; the
  language and the source both count).
- Records only, no behaviour beyond the above, no references to Core or Infrastructure.

## Out of scope

Serialisation (M3-05, M3-06), the engine port (M3-07), UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Domain.Tests`: `ScriptLanguage` text round trip and unknown/case handling; `ScriptBlock.IsEmpty`
      for `""`, spaces, newlines and real code; `ScriptSet.Get/With` for both phases; `Empty` equality;
      `HttpRequestDefinition.IsEquivalentTo` and `FolderDefinition.IsEquivalentTo` differ when only the source or only the
      language of one block differs; `Create` yields `ScriptSet.Empty`.
- [x] The whole solution still builds (existing callers untouched), 0 warnings, every public member has XML docs, all
      tests green.

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

`feat(domain): add script model to requests and folders`
