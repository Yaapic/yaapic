# M6-14: Core: run plan and result model

Status: todo
Platform: any
Depends on: M6-07, M6-09

## Goal

The types shared by the runner, its reports, the view models and the monitors (ADR 0018), and the builder that lists a
folder's requests in run order.

## Design decisions

- Read first: ADR 0018 ("What runs", "Options", "Results"), `src/Yaapic.Domain/Collections/CollectionNode.cs`, `src/Yaapic.Domain/Scripting/TestResult.cs`,
  `ConsoleEntry.cs`, M6-07 (`IterationDataRow`).
- Files in `src/Yaapic.Core/Runner/` (all public, records unless stated):
  - `RunItem(HttpRequestDefinition Request, string Path)`; `Path` = the folder names from the run folder (included) down to the
    request's parent, joined with `/`.
  - `RunOptions(int Iterations = 1, int DelayMs = 0, bool StopOnFailure = false, bool PersistVariables = true, ScriptLimits? Limits = null)`
    with `static RunOptions Default` and `void Validate()` throwing `ArgumentOutOfRangeException` outside 1..10000 iterations or
    0..60000 ms.
  - `RunPlan(string Name, IReadOnlyList<RunItem> Items, RunOptions Options, IReadOnlyList<IterationDataRow> Data)`;
    `IterationDataRow RowFor(int iteration)` (row `i`, the last row past the end, `IterationDataRow.Empty` without data).
  - `RunPlanBuilder.ListRequests(CollectionNode folder)` (static) -> `IReadOnlyList<RunItem>`: depth first in `Children` order;
    throws `ArgumentException` for a request node.
  - `RunRequestOutcome` enum: `Passed`, `Failed`, `Error`, `Skipped`.
  - `RunRequestResult(Guid RequestId, string Name, string Path, HttpMethodKind Method, string Url, RunRequestOutcome Outcome,
    int? StatusCode, TimeSpan Duration, long SizeBytes, IReadOnlyList<TestResult> Tests, IReadOnlyList<ConsoleEntry> Console, string? Error)`;
    `Url` is the request's URL template.
  - `RunIterationResult(int Index, IReadOnlyList<RunRequestResult> Requests)`.
  - `RunStatus` enum: `Completed`, `Stopped`, `StoppedOnFailure`, `Canceled`, `Failed`.
  - `RunResult(string Name, DateTimeOffset StartedAt, TimeSpan Duration, RunStatus Status, string? Error, RunOptions Options,
    int DataRows, IReadOnlyList<RunIterationResult> Iterations)`.
  - `RunSummary` (record) with `static RunSummary From(RunResult result)`: `Sent` (outcomes other than `Skipped` that have a status
    code), `Skipped`, `Errors`, `FailedRequests` (`Failed`), `TestsPassed`, `TestsFailed`, `AverageResponseTime` (mean `Duration` of
    results with a status code; zero when none), `TotalDuration` (= `result.Duration`).
  - `RunProgress(int Iteration, int IterationCount, int Completed, int Total, RunRequestResult? Last, ScriptedSendReport? LastReport)`
    (`Total` = iterations x items, an estimate when `setNextRequest` is used; `LastReport` lets the view model feed the console panel).
  - `static RunRequestOutcome RunOutcomes.From(ScriptedSendReport report)` (in `RunOutcomes.cs`): `Skipped` when `report.Skipped`;
    `Error` when `AbortedBy` is set or the send failed; `Failed` when a test failed; else `Passed`.

## Out of scope

Running (M6-16), variable state (M6-15), reports (M6-18, M6-19).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests/Runner/`: `ListRequests` on a nested tree (order, paths, empty folder); `RowFor` with no data, inside and
  past the end; `Validate` bounds; `RunSummary.From` on a hand-built result with every outcome; `RunOutcomes.From` for each case.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the run plan and result model`
