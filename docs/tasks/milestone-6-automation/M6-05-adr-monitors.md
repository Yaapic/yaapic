# M6-05: ADR 0021: monitors

Status: done
Platform: any
Depends on: M6-01

## Goal

Write ADR 0021, the reference for the monitor cards (definition, schedule, run history, notifications, UI). Docs only. The
decisions below are final.

## Design decisions (to record in the ADR)

- **What a monitor is.** A scheduled run of a folder (ADR 0018) **inside Yaapic, while the app is open on this machine**.
  There is no cloud and no background service (running while the app is closed is the blocked card M6-63).
- **Definition** in the workspace (shared through Git): `monitors/<slug>.monitor.yaml`, VYaml DTOs, `version: 1`: `id`, `name`,
  `folderId` (GUID of the folder to run), `environmentId` (GUID or absent = no environment; never "the active one", so a
  monitor behaves the same whatever the user selects), `intervalMinutes` (one of 5, 10, 15, 30, 60, 180, 360, 720, 1440; default 60),
  `enabled` (default true). Teammates who open the workspace run the enabled monitors too; the docs say so.
- **Run.** A monitor run is a Runner run with 1 iteration, no data, no delay, stop on failure off, **variable changes never
  persisted**, the monitor's environment (loaded by id from the environment store; secret values come from the vault when it is
  unlocked, otherwise they stay blank and the run usually fails, which the history shows). Runs are not recorded in the request
  history; their console lines are not sent to the console panel.
- **Status of a run**: `Passed` (every request `Passed` or `Skipped`), `Failed` (at least one `Failed` or `Error` request) or
  `Error` (the run could not start: folder or environment not found, or the run ended `Failed`, e.g. a `setNextRequest` loop).
- **History** (per user, never committed): `.yaapic/monitor-runs.jsonl`, one JSON line per run (source-generated JSON context):
  `monitorId`, `at` (UTC), `durationMs`, `status`, `requests`, `failedRequests`, `testsPassed`, `testsFailed`, `message`
  (error or first failure), `failures` (at most 20: request name, outcome, status code, first failed test name or error). At most
  200 runs kept per monitor (oldest dropped on append). Deleting a monitor deletes its history lines.
- **Schedule.** A scheduler checks every 30 seconds (`TimeProvider`, `PeriodicTimer`, as `UpdateViewModel` does). A monitor is
  due when it is enabled and `now >= lastRun.At + interval`, or when it never ran. One run at a time across all monitors
  (a queue in due order); a monitor is never queued twice. After the app was closed, an overdue monitor runs once (no catch-up).
  "Run now" queues a run even when disabled. The scheduler starts with the shell and stops (cancelling the current run) when
  the shell closes.
- **Notifications.** A failed or erroneous run adds a console system line "Monitor '{0}' failed: {1}" (warning level) and raises
  `MonitorRunCompleted`; the status bar shows "Monitors: {n} failing" (enabled monitors whose last run is not `Passed`; hidden when 0),
  which opens the Monitors window. Operating-system notifications are out of scope.
- **UI.** A Monitors window (non-modal, one at a time) from the palette ("Monitors"), the folder context menu ("Monitor
  folder...") and the status bar item: monitor list (name, folder, environment, interval, enabled toggle, last status and time,
  next run), editor (name, folder, environment, interval), Run now, Delete, and the history of the selected monitor (last 50 runs:
  time, status, duration, requests, failures; selecting a run shows its failures).
- **Out of scope**: running while the app is closed (M6-63), cron expressions, e-mail/webhook/OS notifications, per-monitor data
  files, regions, uptime charts.

## Out of scope

Code, UI.

## Acceptance criteria

- [x] `docs/adr/0021-monitors.md` contains every decision above (status Accepted; options considered: an OS scheduler task or
  a background service versus in-app scheduling, the active environment versus a fixed one, storing full run results versus
  summaries); `docs/adr/README.md` lists it.
- [x] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0021 monitors`
