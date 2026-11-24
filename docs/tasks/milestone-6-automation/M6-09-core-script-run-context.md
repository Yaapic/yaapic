# M6-09: Core: run context for scripts (iteration, data, execution control)

Status: todo
Platform: any
Depends on: M6-06

## Goal

Give script hosts the run context (iteration, iteration count, data row) and the execution control
(`setNextRequest`, `skipRequest`) of ADR 0018, and make `ScriptedRequestSender` honour a skip. Engines are wired in M6-10 to M6-12.

## Design decisions

- Read first: ADR 0018 ("Script API"), ADR 0009, `src/Yaapic.Core/Scripting/` (`IScriptHost`, `ScriptHost`, `ScriptInfo`,
  `ScriptedSendInput`, `ScriptedSendReport`, `ScriptedRequestSender`), `src/Yaapic.ViewModels/Responses/ResponsePanelViewModel.cs`
  (`ShowScripted`), `tests/Yaapic.TestDoubles/FakeScriptEngine.cs`.
- `ScriptInfo` gains two optional positional parameters at the end: `int Iteration = 0`, `int IterationCount = 1`.
- New `src/Yaapic.Core/Scripting/ScriptRunContext.cs`: `public sealed record ScriptRunContext(int Iteration, int IterationCount,
  IReadOnlyList<KeyValueEntry> Data, bool InRunner)` with `public static ScriptRunContext Single { get; } = new(0, 1, [], false)`.
- New `src/Yaapic.Core/Scripting/ScriptExecutionControl.cs` (public sealed class, one per send, shared by its hosts):
  `bool SkipRequested`, `bool NextRequestSet`, `string? NextRequest`, `IReadOnlyList<string> Warnings`;
  `void SetNextRequest(string? nameOrId, bool inRunner)` (outside the runner: adds `Strings.ScriptSetNextRequestOutsideRunner`
  once and changes nothing else; inside: `NextRequestSet = true`, `NextRequest = nameOrId`, last call wins);
  `void SkipRequest(ScriptPhase phase)` (post-response: throws `InvalidOperationException(Strings.ScriptSkipRequestPostResponse)`;
  pre-request: `SkipRequested = true`).
- `IScriptHost` gains `IReadOnlyList<KeyValueEntry> IterationData { get; }`, `void SetNextRequest(string? nameOrId)` and
  `void SkipRequest()`. `ScriptHost` takes `ScriptRunContext run` and `ScriptExecutionControl control` (constructor parameters
  after `info`, before `send`) and forwards to them; its `Info` carries the run's iteration and count. Update every other
  `IScriptHost` implementation in `src/` and `tests/` (`rtk grep -rn "IScriptHost" src tests --include=*.cs`).
- `ScriptedSendInput` gains `ScriptRunContext? Run = null` (last optional parameter; null means `Single`). The sender builds one
  `ScriptExecutionControl` per send and passes `run` and `control` to every host it creates.
- `ScriptedRequestSender`: after each pre-request block, when `control.SkipRequested`: stop, send nothing, run no post-response block
  and return a report with `Report = null`, `AbortedBy = null`, `SentDefinition = null`, `Changes = overlay.Changes` (changes made
  before the skip are kept), `Skipped = true`. Warnings returned = `overlay.Warnings` followed by `control.Warnings`.
- `ScriptedSendReport` gains init-only properties (not positional, so existing constructions compile): `bool Skipped`,
  `bool NextRequestSet`, `string? NextRequest`. Set them from the control in every path (sent, aborted, skipped). `Redact` keeps them
  (it uses `with`).
- `ResponsePanelViewModel.ShowScripted`: when `report.Skipped`: `ResetDisplay()`, `State = ResponseState.Failed`,
  `ErrorMessage = Strings.ResponseSkippedByScript`, then `Tests.Show(report.Runs)`. `RequestEditorViewModel` raises no `Sent` for a
  skipped send (no history entry) and still applies `Changes`.
- Strings (EN and FR): `script.setNextRequestOutsideRunner` = "yaapic.execution.setNextRequest has no effect outside the Runner";
  `script.skipRequestPostResponse` = "yaapic.execution.skipRequest can only be called in a pre-request script";
  `response.skippedByScript` = "Request skipped by a script: nothing was sent.".

## Out of scope

The engine side (`HostOps`, preludes: M6-10 to M6-12), the runner (M6-16, M6-17).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests` with `FakeScriptEngine` replies that call the host: a skip in the second of three pre-request blocks
  stops there, nothing reaches the transport (`FakeHttpTransport` saw no call), no post-response block runs, `Skipped` is true and earlier
  changes are kept; `SkipRequest` in post-response throws the message; `SetNextRequest` inside a run sets `NextRequest` (last call wins,
  `null` allowed and distinguishable through `NextRequestSet`); outside a run it adds one warning and sets nothing; `Info` and
  `IterationData` reflect the `ScriptRunContext`; `Single` is used when `Run` is null.
- [ ] Tests in `Yaapic.ViewModels.Tests`: the response panel shows the skipped message and the tests of the runs; the editor records no
  history for a skipped send.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): give scripts the run context and execution control`
