# M6-45: WinUI: Runner window, results pane

Status: todo
Platform: windows
Depends on: M6-44

## Goal

The right pane of the Runner window: progress, summary, result rows, filter, details and export (ADR 0018, "UI").

## Design decisions

- Read first: M6-21 (members and strings), M6-44 (`ResultsHost`), `Views/ResponsePanelView.xaml` (the Tests tab rows, status colours and
  badges), `Views/ConsolePanelView.xaml` (console line style), the tokens (`ok`, `err`, `warn` brushes).
- In `RunnerView.xaml` (or a `RunnerResultsView.xaml` user control placed in `ResultsHost`), top to bottom:
  1. Progress row: `ProgressBar` (`Progress`, visible while `IsRunning`), `ProgressText`, `StatusText` when done.
  2. Summary tiles (a wrap of small cards): sent, errors, failed, skipped, tests, average, duration.
  3. Filter `SegmentedControl`-like row of toggle buttons (All, Passed, Failed, Skipped) bound to `Filter`; Export JSON and Export JUnit XML
     buttons at the right; `ExportMessage` next to them.
  4. A vertical split: the `VisibleRows` list (iteration number when iterations > 1 as a group-like prefix "#1", outcome badge coloured
     Passed = ok, Failed/Error = err, Skipped = secondary, method badge, name, path, status, duration, size, tests "x/y") and, below
     (or to the right when the window is wider than 1300 px), the details of `SelectedRow`: tests list (pass/fail icon, name, message),
     error, console lines in monospace.
- Lists through `ListMap`. No logic in code-behind.

## Out of scope

Entry points (M6-49).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [ ] Manual smoke (debug call or M6-49): a run of a folder with a failing test and a data file of 3 rows fills the rows with badges, the
  filter works, selecting a row shows tests and console, Stop cancels, both exports save files that open. Record anything not run in Notes.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the runner results pane`
