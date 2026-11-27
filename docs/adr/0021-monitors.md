# ADR 0021: Monitors

Status: Accepted

See also: [ADR 0005](0005-storage-format.md), [ADR 0010](0010-secret-vault.md), [ADR 0018](0018-collection-runner.md). The
monitor cards of milestone 6 implement this ADR; it is their reference.

## Context

Milestone 6 adds monitors: a folder of requests that runs on a schedule and tells the user when it starts failing. The
definition must be shareable through Git like requests, the scheduler must be testable without UI, and a run must behave the
same whatever the user has selected at that moment.

## Options considered

1. **Where scheduling happens.**
   - *In-app scheduling while Yaapic is open (chosen).* Reuses the Runner (ADR 0018), the vault and the environment store;
     no installer step, no extra process, AOT-safe, works the same on every OS.
   - An operating-system scheduled task. Rejected: one mechanism per OS, needs elevated or installer-time setup, and the task
     would need a headless entry point that does not exist (see blocked card M6-62).
   - A background service. Rejected: needs a second process, its own update path and a vault unlock channel; it was card M6-63,
     answered "not now" in 2026-11.
2. **Environment of a run.**
   - *A fixed environment stored in the monitor (chosen).* The monitor behaves the same whatever the user selects.
   - The active environment. Rejected: switching environments would silently change what every monitor tests.
3. **What the history stores.**
   - *Summaries and at most 20 failures per run (chosen).* Small, safe to keep on disk, enough to see what broke.
   - Full run results (bodies, headers). Rejected: large, and bodies may contain secrets that must not be written to disk.

## Native AOT risks

None new: no reflection, no package. The definition uses VYaml `[YamlObject]` DTOs (source generated), the history uses a
`System.Text.Json` source-generated context, and the scheduler uses `TimeProvider` and `PeriodicTimer`.

## Decision

### What a monitor is

A scheduled run of a folder (ADR 0018) **inside Yaapic, while the app is open on this machine**. There is no cloud and no
background service (running while the app is closed was card M6-63, answered "not now").

### Definition

Workspace file `monitors/<slug>.monitor.yaml`, VYaml DTOs, `version: 1`:

| Field | Meaning |
|---|---|
| `id` | GUID |
| `name` | Display name |
| `folderId` | GUID of the folder to run |
| `environmentId` | GUID, or absent for no environment. Never "the active one" |
| `intervalMinutes` | One of 5, 10, 15, 30, 60, 180, 360, 720, 1440; default 60 |
| `enabled` | Default true |

The file is shared through Git: teammates who open the workspace run the enabled monitors too. The docs say so.

### Run

A monitor run is a Runner run with 1 iteration, no data, no delay, stop on failure off and **variable changes never
persisted**. It uses the monitor's environment, loaded by id from the environment store. Secret values come from the vault
when it is unlocked; otherwise they stay blank and the run usually fails, which the history shows. Runs are not recorded in
the request history, and their console lines are not sent to the console panel.

### Status of a run

- `Passed`: every request is `Passed` or `Skipped`.
- `Failed`: at least one request is `Failed` or `Error`.
- `Error`: the run could not start (folder or environment not found), or the run itself ended `Failed` (for example a
  `setNextRequest` loop).

### History

Per user, never committed: `.yaapic/monitor-runs.jsonl`, one JSON line per run (source-generated JSON context):

`monitorId`, `at` (UTC), `durationMs`, `status`, `requests`, `failedRequests`, `testsPassed`, `testsFailed`, `message` (error
or first failure), `failures` (at most 20: request name, outcome, status code, first failed test name or error).

At most 200 runs are kept per monitor (the oldest are dropped on append). Deleting a monitor deletes its history lines.

### Schedule

A scheduler checks every 30 seconds (`TimeProvider`, `PeriodicTimer`, as `UpdateViewModel` does). A monitor is due when it is
enabled and `now >= lastRun.At + interval`, or when it never ran. One run at a time across all monitors (a queue in due
order); a monitor is never queued twice. After the app was closed, an overdue monitor runs once (no catch-up). "Run now"
queues a run even when the monitor is disabled. The scheduler starts with the shell and stops (cancelling the current run)
when the shell closes.

### Notifications

A failed or erroneous run adds a console system line "Monitor '{0}' failed: {1}" (warning level) and raises
`MonitorRunCompleted`. The status bar shows "Monitors: {n} failing" (enabled monitors whose last run is not `Passed`; hidden
when 0), which opens the Monitors window. Operating-system notifications are out of scope.

### UI

A Monitors window (non-modal, one at a time) opened from the palette ("Monitors"), the folder context menu ("Monitor
folder...") and the status bar item. It shows:

- the monitor list: name, folder, environment, interval, enabled toggle, last status and time, next run;
- the editor: name, folder, environment, interval;
- Run now and Delete;
- the history of the selected monitor (last 50 runs: time, status, duration, requests, failures); selecting a run shows its
  failures.

### Out of scope

Running while the app is closed (M6-63), cron expressions, e-mail, webhook or operating-system notifications, per-monitor
data files, regions, uptime charts.
