# M6-63: Monitors while Yaapic is closed

Status: blocked
Platform: any
Depends on: M6-39

## Goal

Let monitors run on schedule when the Yaapic window is closed (ADR 0021 runs them only while the app is open).

## Open question (product decision, answer before picking this card)

1. **Keep running in the tray / menu bar**: closing the window hides it while a monitor or a mock server is active; a tray icon offers
   Open and Quit. Simple, but a tray icon needs per-OS code (WinUI has no tray API: Win32 `Shell_NotifyIcon`; Avalonia has `TrayIcon`), and
   "close does not quit" surprises people unless it is an explicit setting.
2. **OS scheduler**: register a Windows Task Scheduler task / a systemd user timer / a launchd agent that starts the command-line runner
   (depends on M6-62) for each enabled monitor. Runs even after a reboot, but secrets (the vault needs the user's protector) and notifications
   are harder, and uninstalling must remove the tasks.
3. **A background service** (Windows service / daemon): heavyweight, needs elevation to install. Not recommended.
4. **Not now**: document that monitors run only while Yaapic is open (the current behaviour).

Until answered, no other card depends on this one.

## Acceptance criteria

- [ ] Rewritten after the answer; nothing to implement as is.

## Suggested commit message

`docs: record the decision on running monitors while Yaapic is closed`
