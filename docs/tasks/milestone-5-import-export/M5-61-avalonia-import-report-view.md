# M5-61: Avalonia: import report view

Status: done
Platform: unix
Depends on: M5-50, M5-54, M5-56

## Goal

The Avalonia `UserControl` for an `ImportReportViewModel`, matching M5-56.

## Design decisions

- Read first: M5-56, `Views/CodeGenerationWindow.axaml(.cs)`, `Views/HistoryView.axaml` (list item templates with compiled bindings), `Presentation/BrushMap.cs`, `Presentation/SidebarMap.cs`.
- `Views/ImportReportView.axaml(.cs)` with compiled bindings (`x:DataType="vm:ImportReportViewModel"`, `x:CompileBindings="True"`): the same layout as M5-56 (summary, three `ToggleButton` filters, Copy button, `ItemsControl`/`ListBox` of `VisibleRows` with a severity chip, muted path and wrapped message, empty-state text, min height 160).
  Severity colours through `BrushMap.Severity(ImportSeverity)` (add it here with the same token mapping as the WinUI map: Info neutral, Warning amber, Skipped red; use the Avalonia design token resource keys the neighbouring maps use).
- Strings from `Strings` (exist from M5-50). Avalonia binds `IReadOnlyList<T>` directly: no `ListMap` workaround is needed here.

## Out of scope

The window (M5-62).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the import report view`
