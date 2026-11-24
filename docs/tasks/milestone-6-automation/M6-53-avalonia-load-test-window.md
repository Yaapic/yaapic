# M6-53: Avalonia: Load test window

Status: todo
Platform: unix
Depends on: M6-51, M6-46

## Goal

The Avalonia Load test view, matching the WinUI window of M6-46.

## Design decisions

- Read first: M6-46 (layout and behaviour), M6-26, M6-50 (`LineChart`, `ConfirmAsync`), M6-51 (`RunnerRequestListView`).
- `src/Yaapic.App.Avalonia/Views/LoadTestView.axaml(.cs)`: the left pane (shared request list, data file row, `NumericUpDown`s, profile
  `ComboBox`, ramp-up shown by `IsRampUp`, think time, validation, Start/Stop) and the right pane (elapsed and users, six tiles, 2 x 2
  `LineChart`s, per-request table as a `Grid`-based `ItemsControl` with right-aligned monospace numbers, errors, note, Export JSON and
  `ExportMessage`), in the order of M6-46. Compiled bindings; code-behind forwards events only.

## Out of scope

Entry points (M6-56).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-46; if not possible, say so in Notes and tick only the build and AOT items.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the load test window`
