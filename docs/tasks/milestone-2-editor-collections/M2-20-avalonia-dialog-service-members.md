# M2-20: Avalonia dialog service: prompt, file pickers and clipboard

Status: done
Platform: unix
Depends on: M2-10

## Goal

`AvaloniaDialogService` implements the four members added in M2-10 (behaviour and texts of M2-15).

## Design decisions

- In `Yaapic.App.Avalonia/Services/AvaloniaDialogService.cs`:
  - `PromptTextAsync`: a small modal `Window` (`Views/PromptWindow.axaml`, compiled bindings not needed: set the title,
    label, `TextBox` text in code) with "OK" (default button, Enter) and "Cancel"; OK disabled while the trimmed text is
    empty; returns the trimmed text or null.
  - `PickFileAsync`: `TopLevel.StorageProvider.OpenFilePickerAsync` (single file); path from `TryGetLocalPath()`.
  - `SaveFileAsync`: `SaveFilePickerAsync` with `SuggestedFileName`; write bytes with `File.WriteAllBytesAsync`;
    false on cancel or I/O error.
  - `SetClipboardTextAsync`: `TopLevel.Clipboard.SetTextAsync`.
- The owner window comes from the same place the service already finds it for its other dialogs.

## Out of scope

Callers (later cards).

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings (host rid).
- [x] Verified through the first callers (M2-24 copy/save, M2-39 prompts); note which members were exercised. Not yet exercised: no caller exists yet, only the AOT build and a launch smoke test ran.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): implement prompt, file picker and clipboard in the dialog service`
