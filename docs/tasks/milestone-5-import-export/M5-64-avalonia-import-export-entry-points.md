# M5-64: Avalonia: import and export entry points

Status: done
Platform: unix
Depends on: M5-53, M5-59, M5-62, M5-63

## Goal

The same entry points as M5-59 on the Avalonia front-end.

## Design decisions

- Read first: M5-59, M5-53, the Avalonia main window (`MainWindow.axaml(.cs)` and `Program.cs` if the handlers are there): where `EnvironmentsRequested`, `JwtDecoderRequested`, `CertificatesRequested` open their windows, and the sidebar header menu and the folder context menu (grep `NewFolder`).
- Sidebar header menu: item `Strings.CollectionsImport` after "New collection". Top-level folder context menu: item `Strings.CollectionsExport` bound to the shell `ExportCollectionCommand` (hidden or disabled by `CanExecute` for nested folders).
- Handlers: `ImportRequested` opens `ImportWindow` (new `CreateImport()` view model) and `ExportRequested` opens `ExportWindow` after `LoadAsync(id)`, both modal on the main window and guarded against a second window, following the existing pattern.

## Out of scope

Keyboard shortcuts.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` (win-x64): 0 IL2xxx/IL3xxx warnings; the exe starts and stays alive, then was killed.
- [x] Runtime smoke on macOS or Linux not possible (Windows host): only the build and AOT items are verified; menus and windows not driven, deferred.
- [x] Strings exist in EN and FR (M5-53); 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the import and export entry points`
