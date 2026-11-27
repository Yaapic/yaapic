# M6-60: Milestone 6 docs

Status: done
Platform: any
Depends on: M6-57, M6-58, M6-59, M6-49, M6-56

## Goal

The documentation matches what milestone 6 delivered, and the task index records the milestone summary.

## Design decisions

- Read first: M5-67 (the same card for milestone 5) and the "Milestone 5 summary" block of `docs/tasks/README.md`, ADRs 0018 to 0021,
  `docs/spikes/m6-load.md`, the Notes sections of the M6 cards.
- `docs/architecture.md`: an "Automation (milestone 6)" section with small diagrams in the existing style:
  `RunnerViewModel` -> `CollectionRunner` -> `RunStepExecutor` -> `ScriptedRequestSender` (with `RunVariableState`, `IRunScopeSource`/
  `VariableScopeRunSource`, `IterationDataReader`, `RunReportJson`/`RunReportJUnit`); `LoadTestRunner` (virtual users, `LoadMetrics`);
  `MockServerService` -> `MockResponder`/`MockRouteMatcher` -> `IMockHttpHost` (`TcpMockHttpHost`, `Http11RequestReader`/`Writer`) and
  `IMockServerStore`; `MonitorScheduler` -> `CollectionRunner`, `IMonitorStore`, `IMonitorRunStore`; the shell entries and the tool windows.
  Mention the new data variable layer in the "Request flow" scope list.
- `docs/file-format.md`: check that the M6-28, M6-37 and M6-38 sections are there and consistent; add the variable precedence with the data
  layer in "Variable precedence".
- `docs/dependencies.md`: Milestone 6: **no new package** (sockets, `XmlWriter`, `Utf8JsonWriter` from the framework).
- New `docs/automation.md` (user level): the Runner (what runs, order, options, data files with CSV and JSON examples, the data variables,
  `iterationData`, `setNextRequest`, `skipRequest`, results, reports and how to read JUnit in CI), load tests (profiles, metrics definitions,
  what counts as an error, the confirmation, limits), mock servers (routes, templates, CORS, the log, generating from a folder, 127.0.0.1 only),
  monitors (local only, intervals, history, failures), troubleshooting (port in use, data file errors, setNextRequest loops, locked vault in
  monitors).
- `docs/scripting-compatibility.md`: check the M6-13 rows; add a short "Runner" section linking `automation.md`.
- `README.md` (repository root): the feature list gains the Runner, load tests, mock servers and monitors.
- `docs/tasks/README.md`: the "Milestone 6 summary" block after the card table (delivered, deferred, open items, runtime checks not run) in
  the style of the Milestone 5 summary; the M6 row of the milestone table says `detailed (64 cards, see above)`.

## Out of scope

Screenshots (M6-61).

## Acceptance criteria

- [x] The documents above are updated and their relative links work (open each).
- [x] The summary lists the blocked cards still open (M6-62, M6-63, M6-64) and what is deferred (HTML reports, saved runner configurations,
  spike/peak load profiles, thresholds, HTTPS mocks, mock matching on query/headers/body, OS notifications, cron schedules).
- [x] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs: document the milestone 6 features`
