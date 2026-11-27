# M6-59: End-to-end tests: monitors

Status: done
Platform: any
Depends on: M6-42

## Goal

Schedule and run monitors through the real shell with a controllable clock.

## Design decisions

- Read first: M6-57 (fixture), ADR 0021, M6-39, M6-40, M6-42 (the shell must accept a `TimeProvider`; use the internal `CreateShell`
  overload with a `ManualTimeProvider`, adding the parameter there if M6-42 did not).
- `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone6MonitorsEndToEndTests.cs`:
  1. a monitor on a passing folder with a 5-minute interval runs at start (history line in `.yaapic/monitor-runs.jsonl`), not again after
     4 minutes, again after 5 (advance the manual clock and let the 30 s ticks run);
  2. a monitor with its own environment (different from the active one) sends that environment's `{{baseUrl}}`;
  3. a failing test gives a `Failed` run, the console warning and `shell.MonitorsStatusText` = "Monitors: 1 failing"; after the server is fixed
     a `RunNow` passes and the text is empty;
  4. a disabled monitor does not run on schedule but runs on `RunNow`;
  5. deleting the monitor's folder gives an `Error` run with the message;
  6. two due monitors never run at the same time (a gated `/delay` endpoint);
  7. variable changes made by a monitor run never reach the environment file;
  8. the `monitors/*.monitor.yaml` file round-trips; deleting the monitor removes its history lines;
  9. closing the shell stops the scheduler during a run (no exception, no further history line).

## Out of scope

UI tests.

## Acceptance criteria

- [x] The test class passes locally and in the CI configuration (no external network).
- [x] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for monitors`
