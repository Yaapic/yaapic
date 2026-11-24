# ADR 0018: Collection runner, iteration data and run reports

Status: Accepted

See also: [ADR 0009](0009-scripting.md), [ADR 0008](0008-collections-globals-history-storage.md),
[ADR 0016](0016-import-export-architecture.md). The Runner cards of milestone 6 (M6-06 to M6-21, M6-41 and the UI
cards) implement this ADR; it is their reference.

## Context

Milestone 6 adds a collection Runner: run every request of a folder in order, several times, optionally with a data
file, and export a report. The Runner must behave like the request editor (authentication, certificates, timings,
redaction, script limits) and stay testable without UI or file access.

## Options considered

1. **Where the runner lives.**
   - *In Core, through `ScriptedRequestSender` (chosen).* One pipeline, so a send in the Runner is identical to a send
     in the editor, and the runner logic is pure and unit-testable.
   - A separate pipeline (own transport and script calls). Rejected: duplicates authentication, redaction and script
     limits, and the two would drift.
2. **Runner configuration.**
   - *In-memory setup (chosen).* The workspace has no manual request order and no place for run settings; nothing to
     migrate or merge in Git.
   - Persisting runner configurations. Rejected for this milestone (listed as out of scope).
3. **Data variable layer.**
   - *Between local and environment (chosen).* A row is more specific than an environment but less than a script's local
     value; this matches the Postman iteration data behaviour.
   - Above local, or below the environment. Rejected: a script could not override a row value locally, or an
     environment would silently shadow the data file.

## Native AOT risks

None new: no reflection. Reports use `Utf8JsonWriter` and `XmlWriter` from the framework (no new NuGet package); data
files are parsed by hand (CSV) or with `System.Text.Json` (`JsonDocument`).

## Decision

### Layers

The runner is pure code in `Yaapic.Core.Runner` (no file access, no UI). It sends through the existing
`ScriptedRequestSender` (ADR 0009), so authentication, certificates, timings, redaction and script limits behave exactly
as in the request editor. Data files are picked with `IDialogService.PickFileAsync` and read with the existing
`IImportInputReader.ReadFileAsync` (M5: UTF-8 decoding, BOM removal, size limit); Core only parses the text
(`IterationDataReader.Read(string fileName, string text)`). View models live in `Yaapic.ViewModels.Runner`. No new NuGet
package.

### What runs

A run targets one folder (a top-level folder is a collection, a nested folder is allowed). Its requests are listed depth
first in tree order (`CollectionNode` order: sub-folders first, then requests, each by name), each with its folder path
(`Collection/Sub`). Unfiled requests cannot be run. The setup lets the user untick requests and move them up and down;
this order lives in memory only (never persisted, there is no manual order in the workspace).

### Options

| Option | Range | Default |
|---|---|---|
| Iterations | 1 to 10 000 | 1 (set to the row count when a data file is chosen) |
| Delay between two requests | 0 to 60 000 ms, never after the last request | 0 |
| Stop on failure | on/off | off |
| Keep variable values | on/off | on |

The environment is the active one; switching environments during a run affects the following requests.

### Request outcomes

- `Passed`;
- `Failed`: at least one test failed;
- `Error`: the send failed or a pre-request script aborted it;
- `Skipped`: `yaapic.execution.skipRequest()`.

"Stop on failure" stops the run after the first `Failed` or `Error`. A 4xx or 5xx status alone is not a failure (tests
decide).

### Variables

The run keeps a working copy of the changes scripts make (`RunVariableState`). Before each request the scopes are read
again (`IRunScopeSource.GetScopes`) and the run's changes are applied on top, so later requests see earlier changes even
when "keep variable values" is off. When it is on, the changes of each request are also persisted at once, exactly like
the request editor does (`IScriptVariableApplier` semantics). Runner sends are **not** recorded in the history.

### Iteration data

- CSV (RFC 4180): header row, comma separator, double-quote quoting, `""` escape, CRLF or LF, UTF-8 with or without BOM.
- JSON: an array of objects; string values as is, numbers and booleans as their JSON text, `null` as `""`, objects and
  arrays as compact JSON.
- The format is chosen by extension (`.csv`, `.json`), else by the first non-blank character (`[` is JSON).
- Limits: 10 MiB, 100 000 rows, 1 000 columns.
- Iteration `i` (0-based) uses row `i`, or the last row when `i` is past the end (Postman behaviour).
- A row is an ordered list of name/value pairs.

### Data variable layer

A new layer between local and environment: resolution order is local, **data**, environment, collection, globals,
dynamic. `{{name}}` in a request resolves from the current row. Scripts read it through `yaapic.variables.get` (layered
view) and `yaapic.iterationData`; it is read-only to scripts. Outside a run the layer is empty.

### Script API

JavaScript and Lua, `pm` alias included. This replaces the "not supported" stubs of ADR 0009.

- `yaapic.iterationData.get(name)` (string or `undefined`/`nil`), `.has(name)`, `.toObject()`, `.toJSON()` (same as
  `toObject`). Lua alias `yaapic.iteration_data`, with the Lua naming already used by the variable scopes.
- `yaapic.info.iteration` (0-based) and `yaapic.info.iterationCount`; outside a run 0 and 1 (unchanged).
- `yaapic.execution.setNextRequest(nameOrId)`:
  - after the current request (all its post-response scripts included), jump to the request of the run whose id text
    (GUID "D" format) or else whose name (ordinal, first in run order) matches;
  - `null`/`nil` **stops the whole run** after the current request (Postman);
  - the last call of a request wins; it may be called in pre-request or post-response scripts;
  - an unknown name ends the run with status `Failed` and the error "setNextRequest: no request named '{0}' in this run";
  - outside the runner the call has no effect and adds the console warning "yaapic.execution.setNextRequest has no
    effect outside the Runner";
  - `postman.setNextRequest` is an alias;
  - loop guard: more than 1 000 sends in one iteration stop the run with `Failed` and "The run was stopped: more than
    1000 requests in one iteration (setNextRequest loop?)".
- `yaapic.execution.skipRequest()`:
  - pre-request only; the remaining pre-request blocks do not run, nothing is sent, no post-response block runs, the
    outcome is `Skipped`;
  - works in the request editor too (the response panel says "Request skipped by a script");
  - called in a post-response script it throws "yaapic.execution.skipRequest can only be called in a pre-request script".
- Still unsupported: `yaapic.execution.location`, `yaapic.execution.runRequest`, `yaapic.vault`, `yaapic.visualizer`,
  `yaapic.require`.

### Results

- `RunResult`: start time, duration, status (`Completed`, `Stopped` by `setNextRequest(null)`, `StoppedOnFailure`,
  `Canceled` or `Failed`), error text, iterations.
- `RunIterationResult`: index, request results.
- `RunRequestResult`: request id, name, folder path, method, URL **template** (never the resolved URL, so no secret leaks
  into a report), outcome, status code, duration, size, tests, console entries, error.
- `RunSummary`: counts of sent, skipped, errors, failed requests, tests passed and failed, average response time and
  total duration.

Console lines and test messages are already redacted by `ScriptedRequestSender`.

### Reports

- JSON: own schema `"schema": "yaapic-run/1"`, deterministic, UTF-8 without BOM, `\n`.
- JUnit XML: one `<testsuite>` per request per iteration named `Iteration {n} / {path}/{name}`, one `<testcase>` per
  test, a request `Error` as a `<testcase name="request">` with `<error>`.
- Saved through `IDialogService.SaveFileAsync`. HTML reports are out of scope.

### Console

Every run's console lines, network lines and warnings go to the shared console panel, as for a single send.

### UI

A Runner window (non-modal, one at a time) opened from the folder context menu ("Run folder..."), the palette ("Run
collection...") and the status bar "Runner" item (which shows the progress `3/20` while a run is going).

- Setup on the left: requests with checkboxes and up/down buttons, options, data file with a preview of the column names
  and the first 5 rows.
- Results on the right: summary, iterations, request rows with outcome badges, filter All/Passed/Failed/Skipped, details
  of the selected row (status, time, size, tests, console, error).
- Run/Stop and Export buttons.

Only one run at a time; closing the window does not stop the run.

### Out of scope for the milestone

A command-line runner (blocked card M6-62), scheduled runs outside the app (M6-63), HTML reports, saving runner
configurations, running unfiled requests, parallel runs, "run selected requests" from the tree.
