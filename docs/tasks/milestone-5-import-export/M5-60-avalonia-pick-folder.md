# M5-60: Avalonia: folder picker

Status: done
Platform: unix
Depends on: M5-49, M5-54, M5-55

## Goal

`AvaloniaDialogService.PickFolderAsync` opens the platform folder picker, matching M5-55.

## Design decisions

- Read first: M5-55, `src/Yaapic.App.Avalonia/` `AvaloniaDialogService` (how `PickFileAsync` gets the `TopLevel` and its `StorageProvider`).
- Implement `PickFolderAsync(string title)` with `topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false })`; return `folders[0].TryGetLocalPath()` (null when canceled or the path is not local). Exceptions are caught and return null.

## Out of scope

The import window (M5-62).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [x] 0 warnings, all tests green; note that no UI reaches the picker until M5-62.

## Suggested commit message

`feat(avalonia): add the folder picker`
