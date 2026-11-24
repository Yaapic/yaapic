# M6-51: Avalonia: Runner window, setup pane

Status: todo
Platform: unix
Depends on: M6-50, M6-44

## Goal

The Avalonia Runner view and its setup pane, matching the WinUI pane of M6-44.

## Design decisions

- Read first: M6-44 (layout: the numbered list of controls and behaviours, to reproduce in the same order), M6-20, M6-21, M6-50,
  `src/Yaapic.App.Avalonia/Views/ExportWindow.axaml` and `ImportWindow.axaml` (compiled bindings, form layout), `Presentation/BrushMap.cs`
  (method colours).
- `src/Yaapic.App.Avalonia/Views/RunnerView.axaml(.cs)` (`UserControl`, `x:DataType="vm:RunnerViewModel"`, compiled bindings): a grid with a
  `GridSplitter`, the left pane exactly as M6-44 items 1 to 6 (`ListBox` with `CheckBox`, method badge, name, path, up/down buttons;
  `NumericUpDown`s for iterations and delay; check boxes; data file row with a preview `ItemsControl`/`DataGrid`-free grid of `TextBlock`s;
  validation line; Run/Stop) and a named right `Border` `ResultsHost` for M6-52. Extract the request list into
  `RunnerRequestListView.axaml` so that the load-test window (M6-53) reuses it.
- Code-behind forwards events only.

## Out of scope

Results (M6-52), entry points (M6-56).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-44; if not possible, say so in Notes and tick only the build and AOT items.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the runner window setup pane`
