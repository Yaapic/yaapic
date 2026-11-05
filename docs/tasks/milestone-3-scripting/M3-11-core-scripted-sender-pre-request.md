# M3-11: Core: scripted request sender, pre-request stage

Status: todo
Platform: any
Depends on: M3-08, M3-09, M3-10

## Goal

`ScriptedRequestSender`, the use case that wraps `RequestSender` with scripts. This card builds the host and the
pre-request stage and the abort policy; M3-12 adds the post-response stage.

## Design decisions

- Read first: ADR 0009 (order, error policy, limits), `src/Yaapic.Core/Http/RequestSender.cs`, M3-07 to M3-10.
- `Yaapic.Core.Scripting`:
  - `public sealed record ScriptedSendInput(HttpRequestDefinition Request, VariableScopes Scopes, ScriptChain Chain,
    bool HasEnvironment, bool HasCollection, ScriptLimits? Limits = null)` (`Chain` already contains the request step; use
    `ScriptChain.WithRequest`; `Limits` null means `ScriptLimits.Default`).
  - `public sealed record ScriptedSendReport(SendReport? Report, IReadOnlyList<ScriptRunResult> Runs,
    IReadOnlyList<VariableChange> Changes, IReadOnlyList<string> Warnings, ScriptRunResult? AbortedBy,
    HttpRequestDefinition? SentDefinition)` (the definition after pre-request mutation, null when aborted) with computed
    `IReadOnlyList<ConsoleEntry> Console` (all runs, in order) and `IReadOnlyList<TestResult> Tests`; `Report` is null when
    `AbortedBy` is set.
  - `internal sealed class ScriptHost : IScriptHost` wrapping a `ScriptVariableOverlay`, a `ScriptRequest`, an optional
    `ScriptResponse`, `ScriptInfo`, and a `Func<ScriptRequest, ScriptSendResult>` for `Send` (wired by M3-13; until then it
    returns `new(null, "pm.sendRequest is not available yet")`).
  - `public sealed class ScriptedRequestSender(RequestSender sender, ScriptEngineRegistry engines, TimeProvider time)` with
    `Task<ScriptedSendReport> SendAsync(ScriptedSendInput input, CancellationToken ct)`.
- Pre-request stage: for each step of the chain in order whose `PreRequest` block is not empty: create a `ScriptHost` that
  shares **one** overlay and **one** current `ScriptRequest` for the whole stage (so a collection script's header is visible
  to the request script), `Origin = "pre-request · <step origin>"`, run `engines.Get(block.Language).RunAsync(...)`.
  A system `ConsoleEntry` (`Source.System`, `Origin`) is **not** added here; the console view model adds the group header.
- After the stage: `definition = scriptRequest.ApplyTo(input.Request)` (an `InvalidOperationException` from `ApplyTo`, for
  example an unknown method, becomes a failed run with `ScriptRunStatus.Failed` and the message, origin of the last step),
  `scopes = overlay.ToScopes()`, then `sender.SendAsync(definition, scopes, ct)`.
- **Abort policy** (ADR 0009): the first run whose `Status` is not `Succeeded` (including `Unavailable`, `TimedOut`,
  `MemoryLimit`) stops the chain: nothing is sent, `Report = null`, `AbortedBy = run`, `Changes` is **empty** (discarded),
  `Runs` holds every run executed so far including the failed one. `Canceled` also aborts (and `ct` cancelled means the
  returned report is aborted by a `Canceled` run, never an exception).
- When no step has a non-empty pre-request block, no engine is touched and the behaviour equals plain
  `RequestSender.SendAsync` (an `Empty` chain costs nothing): `Runs` empty, `Changes` empty.
- Overlay `Warnings` are copied to the report.

## Out of scope

Post-response scripts and tests (M3-12), `pm.sendRequest` (M3-13), persisting changes (M3-44), UI.

## Acceptance criteria

- [ ] Tests with `FakeScriptEngine`, `FakeHttpTransport`, in-memory doubles: no scripts -> identical to `RequestSender`
      (same `SendReport`, no runs); order collection, folder, request (assert via invocation origins); two blocks share
      one request (a header added by the first is seen by the second); a variable set in a pre-request block resolves in
      `{{name}}` of the URL sent (assert the transport saw the substituted URL); a mutated method/URL/header/raw body reaches
      the transport; first failing block stops the chain, nothing is sent, changes discarded; `Unavailable` engine aborts
      with the reason; `ApplyTo` failure aborts; token cancelled -> aborted by `Canceled`; warnings propagate; empty blocks
      are not run (fake engine call count).
- [ ] 0 warnings, XML docs, architecture tests green, all tests green.

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

`feat(core): run pre-request scripts before sending`
