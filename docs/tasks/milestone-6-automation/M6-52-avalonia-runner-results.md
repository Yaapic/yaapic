# M6-52: Avalonia: Runner window, results pane

Status: todo
Platform: unix
Depends on: M6-51, M6-45

## Goal

The results pane of the Avalonia Runner view, matching M6-45.

## Design decisions

- Read first: M6-45 (items 1 to 4, to reproduce in the same order), M6-21, M6-51 (`ResultsHost`),
  `src/Yaapic.App.Avalonia/Views/ResponsePanelView.axaml` (Tests tab rows and status colours), `ConsolePanelView.axaml` (console lines).
- `RunnerResultsView.axaml(.cs)` placed in `ResultsHost`: progress row, summary tiles (`WrapPanel`), filter toggle buttons and the two export
  buttons with `ExportMessage`, the rows `ListBox` and the details of `SelectedRow` (tests, error, console in monospace) below it, or to its
  right when the view is wider than 1300 px (an `AdaptiveBehavior`-free approach: bind a `Grid` column/row definition change in code-behind on
  `SizeChanged`, which is view-only logic).
- Compiled bindings; code-behind forwards events and the layout switch only.

## Out of scope

Entry points (M6-56).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-45; if not possible, say so in Notes and tick only the build and AOT items.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the runner results pane`
