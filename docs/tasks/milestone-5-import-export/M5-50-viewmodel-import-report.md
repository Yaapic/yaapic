# M5-50: ViewModel: import report

Status: done
Platform: any
Depends on: M5-04

## Goal

A view model that presents an `ImportReport` (counts, filter by severity, copy as text) for the import dialog and for the result of an export.

## Design decisions

- Read first: M5-04 (`ImportReport`, `ImportEntry`, `ImportSeverity`), a small existing view model for the pattern (`src/Yaapic.ViewModels/Tools/JwtDecoderViewModel.cs`), `IDialogService.SetClipboardTextAsync`.
- `src/Yaapic.ViewModels/Import/ImportReportViewModel.cs` (namespace `Yaapic.ViewModels.Import`), `ObservableObject`, constructor `(IDialogService dialogs)`:
  - `void Load(ImportReport? report)`: replaces the rows (null clears). `IReadOnlyList<ImportReportRow> Rows` (all rows, source order) and `VisibleRows` (the rows whose severity filter is on); `ImportReportRow(ImportSeverity Severity, string SeverityLabel, string Path, string Message)` is a record in the same folder;
    `SeverityLabel` = `Strings.ImportSeverityInfo`, `ImportSeverityWarning` or `ImportSeveritySkipped`.
  - Counts: `InfoCount`, `WarningCount`, `SkippedCount`, `HasRows`, `HasProblems` (warnings or skipped), `Summary` = `Strings.ImportReportSummary(warnings, skipped, info)`, or `Strings.ImportReportEmpty` when no rows.
  - Filters: `ShowInfo`, `ShowWarnings`, `ShowSkipped` (bool, all true by default); changing one raises `VisibleRows`.
  - `CopyCommand` (async, `[RelayCommand]`): puts `AsText()` on the clipboard through `dialogs.SetClipboardTextAsync`; disabled when there are no rows. `string AsText()`: one line per row of **all** rows (the filters do not apply), `[<SeverityLabel>] <Path>: <Message>` (`[<SeverityLabel>] <Message>` when the path is empty), lines joined by `\n`, preceded by the `Summary` line and a blank line.
- Strings (EN and FR): `import.severity.info` = "Note"; `import.severity.warning` = "Warning"; `import.severity.skipped` = "Skipped"; `import.report.summary` = "{0} warnings, {1} skipped, {2} notes"; `import.report.empty` = "Nothing to report."; `import.report.title` = "Report"; `import.report.copy` = "Copy report";
  `import.report.showInfo` = "Notes"; `import.report.showWarnings` = "Warnings"; `import.report.showSkipped` = "Skipped".

## Out of scope

Views (M5-56, M5-61), the import view model.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests/Import`: load with the three severities (counts, summary, labels), empty report, null clears, each filter toggle changes `VisibleRows` (and raises property changed), `AsText` exact text (with and without path), copy puts it on the `FakeDialogService` clipboard, copy disabled when empty.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the import report view model`
