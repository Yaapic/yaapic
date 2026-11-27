# M6-63: Monitors while Yaapic is closed

Status: done
Platform: any
Depends on: M6-39

## Goal

Let monitors run on schedule when the Yaapic window is closed (ADR 0021 runs them only while the app is open).

## Decision (product owner, 2026-11-27)

**Not now** (option 4). Monitors run only while Yaapic is open on this computer, as ADR 0021 already says. Rejected for now: keeping the
app alive in the tray or menu bar, registering operating-system scheduled tasks, a background service.

What it means for users:

- A monitor runs only while the app is open; a monitor that became due while the app was closed runs once at the next start (no
  catch-up), as today.
- Closing the window quits the app and stops every monitor; there is no tray icon and no background process.
- To run a folder on a schedule without the app, use the command-line runner `yaapic-cli` once it ships (M6-62, cards M6-65 to M6-74)
  from a CI pipeline or an operating-system scheduler set up by the user (cron, Task Scheduler). Yaapic does not register such a task.

Not delivered: tray or menu-bar mode, OS scheduled tasks created by Yaapic, a background service, catch-up of missed runs.

Revisit when: users ask for monitors that survive closing the window, or, with `yaapic-cli` shipped, a "schedule this monitor with the
OS" helper becomes cheap (it would also need an answer for secrets, see the M6-62 default).

No code and no new card. Docs: `docs/automation.md` ("Monitors" already says it; "Not delivered" updated), ADR 0021 points here.

## Acceptance criteria

- [x] Rewritten after the answer; nothing to implement.

## Suggested commit message

`docs: record the decision on running monitors while Yaapic is closed`
