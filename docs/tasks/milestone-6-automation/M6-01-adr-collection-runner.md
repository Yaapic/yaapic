# M6-01: ADR 0018: collection runner, iteration data and run reports

Status: todo
Platform: any
Depends on: none

## Goal

Write ADR 0018, the reference for every Runner card of milestone 6 (run model, order, iteration data, script API, variables,
results, reports, UI). Docs only. The decisions below are final.

## Design decisions (to record in the ADR)

- **Layers.** The runner is pure code in `Yaapic.Core.Runner` (no file access, no UI). It sends through the existing
  `ScriptedRequestSender` (ADR 0009), so authentication, certificates, timings, redaction and script limits behave exactly
  as in the request editor. Data files are picked with `IDialogService.PickFileAsync` and read with the existing
  `IImportInputReader.ReadFileAsync` (M5: UTF-8 decoding, BOM removal, size limit); Core only parses the text
  (`IterationDataReader.Read(string fileName, string text)`).
  View models in `Yaapic.ViewModels.Runner`. No new NuGet package (`System.Xml` `XmlWriter` and `Utf8JsonWriter` come from the framework).
- **What runs.** A run targets one folder (a top-level folder is a collection, a nested folder is allowed). Its requests are
  listed depth first in tree order (`CollectionNode` order: sub-folders first, then requests, each by name), each with its
  folder path (`Collection/Sub`). Unfiled requests cannot be run. The setup lets the user untick requests and move them up and
  down; this order lives in memory only (never persisted, there is no manual order in the workspace).
- **Options.** Iterations 1 to 10 000 (default 1; set to the row count when a data file is chosen); delay between two
  requests 0 to 60 000 ms (default 0, never after the last request); stop on failure (default off); keep variable values
  (default on). The environment is the active one; switching environments during a run affects the following requests.
- **A request outcome** is `Passed`, `Failed` (at least one test failed), `Error` (the send failed or a pre-request script aborted
  it) or `Skipped` (`yaapic.execution.skipRequest()`). "Stop on failure" stops the run after the first `Failed` or `Error`.
  A 4xx or 5xx status alone is not a failure (tests decide).
- **Variables.** The run keeps a working copy of the changes scripts make (`RunVariableState`): before each request the scopes
  are read again (`IRunScopeSource.GetScopes`) and the run's changes are applied on top, so later requests see earlier changes
  even when "keep variable values" is off. When it is on, the changes of each request are also persisted at once, exactly
  like the request editor does (`IScriptVariableApplier` semantics). Runner sends are **not** recorded in the history.
- **Iteration data.** CSV (RFC 4180: header row, comma separator, double-quote quoting, `""` escape, CRLF or LF, UTF-8 with or
  without BOM) or JSON (an array of objects; string values as is, numbers and booleans as their JSON text, `null` as `""`,
  objects and arrays as compact JSON). Chosen by extension (`.csv`, `.json`), else by the first non-blank character (`[` is
  JSON). Limits: 10 MiB, 100 000 rows, 1 000 columns. Iteration `i` (0-based) uses row `i`, or the last row when `i` is past
  the end (Postman behaviour). A row is an ordered list of name/value pairs.
- **Data variable layer.** A new layer between local and environment: resolution order is local, **data**, environment,
  collection, globals, dynamic. `{{name}}` in a request resolves from the current row. Scripts read it through
  `yaapic.variables.get` (layered view) and `yaapic.iterationData`; it is read-only to scripts. Outside a run the layer is empty.
- **Script API** (JavaScript and Lua, `pm` alias included; replaces the "not supported" stubs of ADR 0009):
  - `yaapic.iterationData.get(name)` (string or `undefined`/`nil`), `.has(name)`, `.toObject()`, `.toJSON()` (same as
    `toObject`); Lua `yaapic.iteration_data` alias with the Lua naming already used by the variable scopes.
  - `yaapic.info.iteration` (0-based) and `yaapic.info.iterationCount`; outside a run 0 and 1 (unchanged).
  - `yaapic.execution.setNextRequest(nameOrId)`: after the current request (all its post-response scripts included), jump to
    the request of the run whose id text (GUID "D" format) or else whose name (ordinal, first in run order) matches; `null`/`nil`
    **stops the whole run** after the current request (Postman). The last call of a
    request wins; it may be called in pre-request or post-response scripts. An unknown name ends the run with status `Failed` and
    the error "setNextRequest: no request named '{0}' in this run". Outside the runner the call has no effect and adds the console
    warning "yaapic.execution.setNextRequest has no effect outside the Runner". `postman.setNextRequest` is an alias.
    Loop guard: more than 1 000 sends in one iteration stop the run with `Failed` and "The run was stopped: more than 1000
    requests in one iteration (setNextRequest loop?)".
  - `yaapic.execution.skipRequest()`: pre-request only; the remaining pre-request blocks do not run, nothing is sent, no
    post-response block runs, the outcome is `Skipped`. Works in the request editor too (the response panel says "Request
    skipped by a script"). Called in a post-response script it throws "yaapic.execution.skipRequest can only be called in a
    pre-request script".
  - Still unsupported: `yaapic.execution.location`, `yaapic.execution.runRequest`, `yaapic.vault`, `yaapic.visualizer`, `yaapic.require`.
- **Results.** `RunResult` (start time, duration, status `Completed`, `Stopped` (by `setNextRequest(null)`), `StoppedOnFailure`,
  `Canceled` or `Failed`, error text, iterations), `RunIterationResult` (index, request results), `RunRequestResult` (request id,
  name, folder path, method, URL **template** (never the resolved URL, so no secret leaks into a report), outcome, status code,
  duration, size, tests, console entries, error). `RunSummary` counts sent, skipped, errors, failed requests, tests passed and
  failed, average response time and total duration. Console lines and test messages are already redacted by `ScriptedRequestSender`.
- **Reports.** Export as JSON (own schema `"schema": "yaapic-run/1"`, deterministic, UTF-8 without BOM, `\n`) or JUnit XML (one
  `<testsuite>` per request per iteration named `Iteration {n} / {path}/{name}`, one `<testcase>` per test, a request `Error` as a
  `<testcase name="request">` with `<error>`). Saved through `IDialogService.SaveFileAsync`. HTML reports are out of scope.
- **Console.** Every run's console lines, network lines and warnings go to the shared console panel, as for a single send.
- **UI.** A Runner window (non-modal, one at a time) opened from the folder context menu ("Run folder..."), the palette ("Run
  collection...") and the status bar "Runner" item (which shows the progress `3/20` while a run is going). Setup on the left
  (requests with checkboxes and up/down buttons, options, data file with a preview of the column names and the first 5 rows),
  results on the right (summary, iterations, request rows with outcome badges, filter All/Passed/Failed/Skipped, details of the
  selected row: status, time, size, tests, console, error), Run/Stop and Export buttons. Only one run at a time; closing the
  window does not stop the run.
- **Out of scope for the milestone**: a command-line runner (blocked card M6-61), scheduled runs outside the app (M6-62),
  HTML reports, saving runner configurations, running unfiled requests, parallel runs, "run selected requests" from the tree.

## Out of scope

Code, UI.

## Acceptance criteria

- [ ] `docs/adr/0018-collection-runner.md` contains every decision above (status Accepted, options considered: running in
  Core through `ScriptedRequestSender` versus a separate pipeline, persisting runner configurations versus in-memory setup,
  data layer below versus above the environment); `docs/adr/README.md` lists it.
- [ ] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0018 collection runner`
