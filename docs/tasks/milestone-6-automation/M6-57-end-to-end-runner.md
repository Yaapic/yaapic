# M6-57: End-to-end tests: Runner

Status: todo
Platform: any
Depends on: M6-42

## Goal

Run folders through the real shell, engines and loopback server: iterations, data files, flow control, variables and reports.

## Design decisions

- Read first: `tests/Yaapic.Infrastructure.Tests/EndToEnd/` (`Milestone3ScriptingEndToEndTests`, `Milestone5ImportEndToEndTests`,
  `ScriptedShellFixture`, `LoopbackServer` (echo, `/status/{code}`, `/delay/{ms}`), `TempWorkspace`), M6-42, ADR 0018.
- `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone6RunnerEndToEndTests.cs`. The shell is built like the milestone 3 tests; folders and
  requests are written through the stores (or imported from a small Postman sample, as M5-65 does), then `shell.RunFolderCommand` (or
  `shell.Runner.Open` + `RunCommand`). Data files are written to the temp directory and chosen through `FakeDialogService.PickFileAnswer`.
- Scenarios (assert results, console, stores and files):
  1. a 3-request folder x 2 iterations: 6 rows, order, statuses from the echo server, summary;
  2. a CSV file (3 rows, a quoted comma) drives `{{user}}` in the URL and `yaapic.iterationData.get('user')` in a test (JavaScript) and in Lua;
  3. `yaapic.info.iteration` and `iterationCount` seen by a script;
  4. a post-response script sets `token` in the environment, the next request sends it in a header (echoed); with "keep variable values" on
     the environment file on disk has it after the run; with it off the file is unchanged but the second request still got it;
  5. `setNextRequest` jumps forward, `setNextRequest(null)` stops (`Stopped`), an unknown name fails, a loop hits the guard;
  6. `skipRequest` in a folder pre-request script skips one request (outcome `Skipped`, server not called for it);
  7. stop on failure after a failed test; Stop (cancel) during a `/delay/2000` request;
  8. the JSON and JUnit exports saved through `FakeDialogService.SavedFiles` parse (`DataNodeJson.Parse`, `XmlReader`) and contain no
     resolved secret (a secret environment variable used in a header never appears in either report);
  9. runner sends do not reach the history store;
  10. a request in the editor with `skipRequest()` shows "Request skipped by a script" and nothing is sent.
- Tests that need the native engines follow the skip rules of the milestone 3 end-to-end tests.

## Out of scope

UI tests, load tests and mocks (M6-58), monitors (M6-59).

## Acceptance criteria

- [ ] The test class with the scenarios above passes locally and in the CI configuration (no external network).
- [ ] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for the collection runner`
