# M5-63: Avalonia: export window

Status: done
Platform: unix
Depends on: M5-52, M5-58, M5-60, M5-61

## Goal

The Avalonia Export window, matching the WinUI dialog of M5-58.

## Design decisions

- Read first: M5-58 (controls and behaviour), M5-52, M5-62, `AvaloniaDialogService.SaveFileAsync` (how the save picker is built from the suggested name).
- `Views/ExportWindow.axaml(.cs)` (`ExportViewModel`, compiled bindings), 640 x 520, same layout as M5-58: collection `ComboBox` (hidden when not needed, empty-state text), format `ComboBox`, the environments check list and `IncludeGlobals` for `postman-environment`, the secrets note, saved text, `ImportReportView`, error, busy bar, **Export...** and **Close**.
- The save picker of `AvaloniaDialogService.SaveFileAsync` must handle `.zip` and `.json` and `.yaml` suggested names (file type choices derived from the extension): extend it here if it does not.
- Opened by the shell handler of M5-64 with `shell.CreateExport()` and `LoadAsync(collectionId)`.

## Out of scope

Entry points (M5-64).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M5-58 (not run: Windows host; the AOT exe starts and stays alive, the window entry point is M5-64).
- [x] Strings exist in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the export window`
