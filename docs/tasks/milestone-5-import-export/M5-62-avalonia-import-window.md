# M5-62: Avalonia: import window

Status: todo
Platform: unix
Depends on: M5-51, M5-57, M5-60, M5-61

## Goal

The Avalonia Import window, matching the WinUI dialog of M5-57.

## Design decisions

- Read first: M5-57 (layout, the numbered list of controls and behaviours), M5-51, `Views/CodeGenerationWindow.axaml(.cs)` and `Views/CertificatesWindow.axaml(.cs)` (modal windows owned by the main window, sizing), `Views/ConvertScriptWindow.cs` (a paste window).
- `Views/ImportWindow.axaml(.cs)` (`ImportViewModel`, compiled bindings), 720 x 560, resizable, same controls and order as M5-57 (file and folder buttons with `SourceName`, a 3-line monospace `TextBox` for `PastedText` with a watermark and the Preview button, `ComboBox` of `Formats`, detected text, the include-static-resources `CheckBox` shown by `ShowStaticResourcesOption`,
  counts line, `ImportReportView`, error line, busy `ProgressBar` (indeterminate), **Import** and **Close** buttons). `ViewModel.Reset()` when the window opens.
- Opened by the shell handler of M5-64 as a modal child of the main window with `shell.CreateImport()`.
- Code-behind forwards events only.

## Out of scope

Entry points (M5-64).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M5-57 (all five sources, folder picker, report list, Copy); if not possible, say so in the Notes and tick only the build and AOT items.
- [ ] Strings exist in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the import window`
