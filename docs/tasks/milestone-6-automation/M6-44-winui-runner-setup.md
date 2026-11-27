# M6-44: WinUI: Runner window, setup pane

Status: done
Platform: windows
Depends on: M6-43

## Goal

The Runner window and its left pane (requests, options, data file) bound to `RunnerViewModel.Setup` (ADR 0018, "UI").

## Design decisions

- Read first: ADR 0018 ("UI"), M6-20 and M6-21 (members and strings), M6-43 (`ToolWindow`), `Views/ExportDialog.xaml` and
  `Views/ImportDialog.xaml` (form layout, `ListMap` bindings), `Presentation/ListMap.cs`, `Presentation/MethodBrush.cs`, the design tokens and
  `design/mockups/DSComposants2.dc.html` (lists, fields).
- `Views/RunnerView.xaml(.cs)` (`RunnerViewModel`), a two-column grid: left pane 360 px (resizable with the existing `SplitterHandle`), right
  pane the results (M6-45 fills it; leave a named `Border x:Name="ResultsHost"`). Left pane, top to bottom:
  1. Header: folder name (`Setup.FolderName`, semibold) and `Strings.RunnerSelectedCount(selected, total)`.
  2. "Select all" / "Select none" link buttons.
  3. Request list (`ListView`, through `ListMap`): `CheckBox` (`IsSelected`), method badge (`MethodBrush`), name, path in secondary text;
     up/down icon buttons on the row (`MoveUpCommand`/`MoveDownCommand` with the item); fills the remaining height.
  4. Options: `NumberBox` Iterations, `NumberBox` Delay (ms), `CheckBox` Stop on failure, `CheckBox` Keep variable values.
  5. Data file: "Choose a CSV or JSON file..." button, `Strings.RunnerDataSummary(file, rows, columns)` with a Remove button when `HasData`,
     a small read-only grid of `DataPreview` under `DataColumns` (max 5 rows, horizontal scroll), `DataError` in the error brush.
  6. `ValidationError` line (error brush) and the **Run** / **Stop** buttons (`RunCommand`/`StopCommand`; Run is the accent button).
- Every list is bound through `ListMap` (add `ListMap.RunnerItems`, `ListMap.DataRows` ...) because of the AOT crash noted in M4-68.
- The view is created by the entry point card (M6-49) inside a `ToolWindow` titled `Strings.RunnerTitle`. Code-behind forwards events only.

## Out of scope

Results pane (M6-45), entry points (M6-49).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [x] Manual smoke through a temporary debug call (removed before the commit) or after M6-49 if it is already done: load a folder, untick and
  reorder requests, choose a CSV file (preview shows), invalid values show the validation line, Run starts (results pane empty for now).
  Record anything not run in Notes.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the runner window setup pane`

## Notes

- Verified: Release build (0 warnings), full test suite green, AOT publish with 0 IL2xxx/IL3xxx warnings, the published exe starts and stays alive.
- Not run: the manual UI smoke (folder load, reorder, CSV preview, validation line). `RunnerView` is not instantiated anywhere until the
  entry point card (M6-49); it must be checked then (and in M6-61).
- `ListMap.RunnerItems` passes the live `ObservableCollection` through (a copy would not follow `Move`).
