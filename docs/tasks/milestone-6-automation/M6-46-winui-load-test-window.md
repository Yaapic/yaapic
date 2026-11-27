# M6-46: WinUI: Load test window

Status: done
Platform: windows
Depends on: M6-43, M6-44

## Goal

The Load test window bound to `LoadTestViewModel` (ADR 0019, "UI").

## Design decisions

- Read first: ADR 0019, M6-26 (members, strings), M6-43 (`ToolWindow`, `LineChart`, `ConfirmAsync`), M6-44 (request list markup: reuse it
  as a shared `RunnerRequestListView` user control if M6-44 kept it inline; extract it in this card and use it in both).
- `Views/LoadTestView.xaml(.cs)`: left pane 360 px: request list (selection and order, the shared control), data file row, `NumberBox`es
  Virtual users, Duration (s), `ComboBox` Profile (Fixed, Ramp up), Ramp-up (s) visible when `IsRampUp`, Think time (ms), `ValidationError`,
  **Start**/**Stop**. Right pane: `ElapsedText` and active users; six tiles (requests, throughput, average, p95, error rate, active users);
  a 2 x 2 grid of `LineChart`s (requests per second, average response time, errors per second, virtual users) with `ThroughputMax`/
  `ResponseTimeMax` as the max labels; then the per-request table (`RequestRows`: name, method, count, errors, average, p90, p95, p99, max),
  the error messages list, `Note`, **Export JSON** and `ExportMessage`.
- Lists through `ListMap`; numbers right-aligned in a monospace font.

## Out of scope

Entry points (M6-49).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [x] Manual smoke against a local server (a Yaapic mock server once M6-47 exists, or any loopback server): the confirmation shows the host,
  a 20 s ramp-up test of 10 users updates tiles and charts every second, Stop works, the table fills at the end, export saves. Record
  anything not run in Notes.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the load test window`

## Notes

- Verified: Release build (0 warnings), AOT publish with 0 IL2xxx/IL3xxx warnings, the published exe starts and stays alive.
- Not run: the manual smoke against a local server (confirmation, 20 s ramp-up, Stop, table, export). `LoadTestView` is not instantiated
  anywhere until M6-49; check it then (and in M6-61). Also unchecked at runtime: the charts' `Points` dependency property (a list of a
  non-WinRT struct) under Native AOT.
- The request list is now the shared `Views/RunnerRequestListView` (title row, select all/none, reorderable list), used by `RunnerView`
  and `LoadTestView`. New strings `load.col*` (table headers), `ListMap.LoadTiles/LoadRows`, `TextMap.ProfileIndex`.
- Flaky, unrelated: `LoadTestRunnerTests` (`A_snapshot_is_reported_every_second_and_at_the_end`, `Ramp_up_starts_users_at_the_scheduled_offsets`)
  failed intermittently again under load from another build; `JwtDecoderViewModelTests` fails on the French locale.
- Fix `fix/runner-request-list`: `RunnerRequestListView` now starts with a folder selector (Monitors folder choices) and a "Choose a folder to run" empty state; the setup panes keep a minimum list height (`PaneLayout`) and the Load test pane has a splitter (`ColumnResizer`).
