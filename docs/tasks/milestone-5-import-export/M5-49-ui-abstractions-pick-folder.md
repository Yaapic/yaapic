# M5-49: UI abstractions: pick a folder

Status: done
Platform: any
Depends on: M5-01

## Goal

`IDialogService` can ask for a folder (Bruno import). The platform implementations come with the WinUI and Avalonia cards (M5-55, M5-60).

## Design decisions

- Read first: `src/Yaapic.UI.Abstractions/IDialogService.cs` (default interface methods, e.g. `PickFileAsync`), `tests/Yaapic.TestDoubles/FakeDialogService.cs`.
- Add to `IDialogService`: `Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);` with the XML doc "Asks for an existing folder. The default does nothing; platforms override it." returning the path or `null` when canceled.
- `FakeDialogService`: `public string? PickFolderAnswer { get; set; }` and `PickFolderAsync` returning it; `public List<string> PickFolderTitles { get; } = []` recording the titles.
- Strings: none.

## Out of scope

The WinUI and Avalonia pickers.

## Acceptance criteria

- [x] A test in `Yaapic.ViewModels.Tests` (or `Yaapic.Core.Tests`, wherever `FakeDialogService` is already tested) that the fake records the title and returns the scripted path, and that a minimal `IDialogService` implementation that does not override the method returns null.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green; both App projects still build (the default method means no change there).

## Suggested commit message

`feat(ui): add PickFolderAsync to the dialog service`
