# M6-55: Avalonia: Monitors window

Status: todo
Platform: unix
Depends on: M6-50, M6-48

## Goal

The Avalonia Monitors view, matching the WinUI window of M6-48.

## Design decisions

- Read first: M6-48 (layout and behaviour), M6-40, M6-50, M6-54 (master-detail look to match).
- `src/Yaapic.App.Avalonia/Views/MonitorsView.axaml(.cs)`: local-only note, monitor list (name, folder, interval, enabled `ToggleSwitch`,
  status dot, last run, running indicator, New, Delete), editor (name, folder, environment, interval, Save/Revert, Run now, next run),
  history list and failures of the selected run. Compiled bindings; code-behind forwards events only.

## Out of scope

Entry points and the status bar (M6-56).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-48; if not possible, say so in Notes and tick only the build and AOT items.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the monitors window`
