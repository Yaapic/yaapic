# M5-56: WinUI: import report view

Status: todo
Platform: windows
Depends on: M5-50, M5-54

## Goal

A reusable `UserControl` that shows an `ImportReportViewModel`: summary line, severity filters, the list of entries, Copy.

## Design decisions

- Read first: M5-50, `src/Yaapic.App.WinUI/Views/CodeGenerationDialog.xaml(.cs)` and `JwtDecoderDialog.xaml` (dialog layout, spacing, tokens), `Presentation/ListMap.cs` and the **M4-68 notes in `docs/tasks/milestone-4-advanced-auth-response/M4-68-winui-code-generation-dialog.md`**: under Native AOT, `ItemsSource` must be
  bound through `ListMap` (an `ObservableCollection<T>` built from the read-only list, because CsWinRT cannot marshal `IReadOnlyList<T>` as a bindable vector); add `ListMap.ReportRows(IReadOnlyList<ImportReportRow>)` and bind to it. Also read `Presentation/BrushMap.cs`, `MethodBrush.cs` for the severity colours.
- `Views/ImportReportView.xaml(.cs)`, `DataContext`-style `ViewModel` property of type `ImportReportViewModel` (set by the host dialog): a header row with `Summary` and the three filter `ToggleButton`s (`ShowInfo`, `ShowWarnings`, `ShowSkipped`, labels `Strings.ImportReportShowInfo`...) and a `Copy` button (`CopyCommand`, label `ImportReportCopy`);
  below, a `ListView` of `VisibleRows` (through `ListMap`): per row a coloured severity chip (`SeverityLabel`; colours from the existing tokens: Info neutral, Warning amber, Skipped red; add `BrushMap.Severity(ImportSeverity)` with unit-test-free resource lookups like the existing maps), the `Path` in the muted brush (hidden when empty) and the `Message` wrapped.
  When there are no rows show `Strings.ImportReportEmpty` instead of the list. Min height 160, the list scrolls inside the view.
- Colours and fonts only through existing theme resources; strings only through `Strings` (EN and FR exist from M5-50).

## Out of scope

The import dialog (M5-57), the export result display (M5-58 reuses this view).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards). The view is not reachable yet, so the check that matters (a non-empty list under AOT) happens in M5-57: state that in the Notes.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the import report view`
