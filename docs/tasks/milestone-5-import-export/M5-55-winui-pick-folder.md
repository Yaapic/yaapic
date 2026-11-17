# M5-55: WinUI: folder picker

Status: todo
Platform: windows
Depends on: M5-49, M5-54

## Goal

`WinUIDialogService.PickFolderAsync` opens the Windows folder picker.

## Design decisions

- Read first: `src/Yaapic.App.WinUI/` `WinUIDialogService` (how `PickFileAsync` obtains the window handle and initializes the picker), M5-49.
- Implement `PickFolderAsync(string title)` with `Windows.Storage.Pickers.FolderPicker` (`SuggestedStartLocation = DocumentsLibrary`, `FileTypeFilter.Add("*")`, `InitializeWithWindow.Initialize(picker, hwnd)` exactly like the file picker); returns `folder.Path` or null. Exceptions from the picker (no window, COM errors) are caught and return null.
- Native AOT: only the WinRT types already used by the file picker (CsWinRT projections); no new API surface beyond `FolderPicker`.

## Out of scope

The import dialog (M5-57).

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe is part of M5-57 (the picker is opened from the import dialog); note here that no UI reaches it yet.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the folder picker`
