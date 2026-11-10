# M3-43: Request editor: send through scripts

Status: done
Platform: any
Depends on: M3-12, M3-39, M3-41, M3-42, M3-10

## Goal

`RequestEditorViewModel.SendCommand` runs the script chain around the send, fills the response panel and the console, and
raises `Sent` exactly as before for history.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Requests/RequestEditorViewModel.cs` (`SendCommand`, `FromDefinition`, `CreateNew`,
  `Sent`, `RequestSentEventArgs`), `VariableScopeProvider.cs`, M3-11/M3-12 (input/report), the call sites in
  `AppComposition` and `tests/Yaapic.ViewModels.Tests`.
- `public sealed record ScriptSendServices(ScriptedRequestSender Sender, IScriptChainProvider Chains, ConsoleViewModel Console,
  IScriptVariableApplier Variables)` in `Yaapic.ViewModels.Requests` (the `IScriptVariableApplier` interface is declared here with
  `Task<IReadOnlyList<string>> ApplyAsync(Guid requestId, IReadOnlyList<VariableChange> changes)` and implemented by M3-44; until
  then a test double records calls: `FakeScriptVariableApplier` in `Yaapic.TestDoubles`).
- `RequestEditorViewModel.FromDefinition(...)` and `CreateNew(...)` gain an optional last parameter
  `ScriptSendServices? scripting = null`. **Null keeps today's behaviour exactly** (plain `RequestSender` path, no scripts, tests
  unchanged).
- With services: `SendCommand` builds `ScriptedSendInput` (current definition, `scopes.GetScopes(Id)`, chain =
  `Chains.GetAncestors(Id).WithRequest(DisplayName, Scripts.ToSet())`, `HasEnvironment`/`HasCollection` from
  `GetWritableScopes(Id)`), awaits `Sender.SendAsync`, then on the UI thread: `Console.AppendReport(report)`,
  `Response.ShowScripted(report)`, and `await Variables.ApplyAsync(Id, report.Changes)` (its error strings become `Warn` console
  lines `"Could not save variable changes: <message>"`); raises `Sent` with `(definition-as-sent, report.Report)` **only when
  `report.Report` is not null** (an aborted send is not history). The definition passed to `Sent` is the one after pre-request
  mutation (`report` exposes it: add `HttpRequestDefinition? SentDefinition` to `ScriptedSendReport` in M3-11's type if absent;
  otherwise use the editor definition and note it).
- Cancel: the existing cancel command cancels the token passed to `SendAsync`; a script in progress is cancelled too, the
  response shows "Request canceled".
- Unresolved variables warning behaviour unchanged (it comes from `report.Report`).
- Busy state: `IsSending` covers pre-request scripts, the send and post-response scripts.

## Out of scope

Persisting variable changes (M3-44), composition wiring (M3-47), views.

## Acceptance criteria

- [x] Tests (`FakeScriptEngine`, `FakeHttpTransport`, doubles): no services -> existing tests pass untouched; with services:
      pre-request header reaches the transport, tests land in `Response.Tests`, console receives header/lines/network lines,
      `Sent` raised once with the mutated definition, aborted pre-request raises no `Sent` and shows the error state, post-response
      error keeps the response, variable changes are handed to the applier, applier errors become console warnings, cancel during
      a slow fake script ends with "Request canceled", `IsSending` true during the whole pipeline.
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

`feat(viewmodels): send requests through the script pipeline`
