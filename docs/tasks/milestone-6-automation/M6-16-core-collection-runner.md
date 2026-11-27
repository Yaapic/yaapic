# M6-16: Core: collection runner (step executor, iterations, delay, stop and cancel)

Status: todo
Platform: any
Depends on: M6-09, M6-15

## Goal

Run a `RunPlan`: every item of every iteration, in order, through `ScriptedRequestSender`, with the delay, stop on failure,
persistence and cancellation of ADR 0018. Flow control (`setNextRequest`, `skipRequest`) is M6-17; this card runs straight through.

## Design decisions

- Read first: ADR 0018, M6-09 (`ScriptRunContext`, report flags), M6-14, M6-15, `src/Yaapic.Core/Scripting/ScriptedRequestSender.cs`,
  `src/Yaapic.ViewModels/Requests/RequestEditorViewModel.cs` (`SendScriptedAsync`: how the input, writable scopes and changes are
  handled for one send; the runner must behave the same).
- `src/Yaapic.Core/Runner/RunStepExecutor.cs` (public sealed; reused by load tests, M6-24):
  constructor `(ScriptedRequestSender sender, IScriptChainProvider chains, IRunScopeSource scopes)`;
  `Task<RunStepResult> ExecuteAsync(RunItem item, RunVariableState state, ScriptRunContext context, bool persist, ScriptLimits? limits, CancellationToken ct)`:
  1. `scopes = state.Overlay(source.GetScopes(id), new IterationDataRow(context.Data))`, `writable = source.GetWritableScopes(id)`;
  2. `ScriptedSendInput(request, scopes, chains.GetAncestors(id).WithRequest(request.Name, request.Scripts), writable has Environment,
     writable has Collection, limits, context)`;
  3. `report = await sender.SendAsync(input, ct)`; `state.Apply(report.Changes)`; when `persist` and there are changes, `PersistAsync`
     and turn each returned error into a warning `Strings.RequestVariableChangesNotSaved(error)`;
  4. `RunRequestResult`: outcome `RunOutcomes.From(report)`; status code, duration, size from `report.Report?.Result.Response`; tests
     `report.Tests`; console = `report.Console`, then `report.NetworkLog`, then one `ConsoleEntry(now, Warn, System, warning)` per
     warning; error = the aborting run's error message (or its status text), else the send failure message, else null.
  `public sealed record RunStepResult(RunRequestResult Result, ScriptedSendReport Report)`; `bool IsCanceled` = the aborting run is
  `Canceled` or the failure kind is `Canceled`.
- `src/Yaapic.Core/Runner/CollectionRunner.cs`: constructor `(RunStepExecutor step, TimeProvider time)`;
  `Task<RunResult> RunAsync(RunPlan plan, IProgress<RunProgress>? progress, CancellationToken ct)`:
  - `plan.Options.Validate()`; empty `Items` returns `Completed` with no iteration.
  - One `RunVariableState` for the whole run. For iteration `i` in `0..Iterations-1`, context
    `new ScriptRunContext(i, Iterations, plan.RowFor(i).Values, InRunner: true)`; each item in order.
  - Between two consecutive sends (also across iterations, never after the last) wait `DelayMs` with `Task.Delay(TimeSpan, time, ct)`.
  - After each step report progress (`Completed` counts executed steps). Stop on failure: when on and the outcome is `Failed` or
    `Error`, end with `StoppedOnFailure` (the current iteration's results so far are kept).
  - Cancellation (token or a canceled step) ends with `Canceled` and never throws; results so far are kept, a canceled step is not added.
  - `StartedAt = time.GetUtcNow()` at start, `Duration` from a `time.GetTimestamp()` pair.
- Runner sends are not reported to the history (there is no history call here; keep it that way).

## Out of scope

`setNextRequest`, `skipRequest` handling and the loop guard (M6-17), reports (M6-18, M6-19), view models.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Runner/` with `FakeHttpTransport` (or a recording transport), `FakeScriptEngine`, `FakeScriptChainProvider`,
  a fake `IRunScopeSource` and `ManualTimeProvider`: three items x two iterations in order; data row per iteration reaches
  `{{column}}` in the URL and `IterationData` in the host; a variable set by a post-response script of item 1 is used by item 2 with
  persist off (and `PersistAsync` not called) and with persist on (called once per changed request); a persist error becomes a
  console warning; delay waits between sends only (advance the manual clock); stop on failure after a failed test; cancellation
  mid-run returns `Canceled` with the completed results; progress reports count and `LastReport`; `RunSummary` of a run.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the collection runner`
