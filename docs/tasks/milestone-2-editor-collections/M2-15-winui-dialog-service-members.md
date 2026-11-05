# M2-15: WinUI dialog service: prompt, file pickers and clipboard

Status: done
Platform: windows
Depends on: M2-10

## Goal

`WinUIDialogService` implements the four members added in M2-10.

## Design decisions

- In `Yaapic.App.WinUI/Services/WinUIDialogService.cs`, override:
  - `PromptTextAsync(title, label, initialText)`: a `ContentDialog` (same `XamlRoot` and style as the existing
    dialogs) with the title, a `TextBox` headed by `label` and pre-filled with `initialText` (all text selected on
    open), primary button "OK" (default, so Enter submits), close button "Cancel". Returns the trimmed text, or null
    on cancel or when the trimmed text is empty (the primary button is disabled while the text is blank).
  - `PickFileAsync(title)`: `FileOpenPicker` (filter `*`), initialised with the main window handle
    (`WinRT.Interop.InitializeWithWindow.Initialize`); returns the path or null.
  - `SaveFileAsync(suggestedName, content)`: `FileSavePicker` with `SuggestedFileName` and the extension of the
    suggested name as the only file type choice (plus "All files"); writes with `File.WriteAllBytesAsync`; returns false
    on cancel or on `IOException`/`UnauthorizedAccessException`.
  - `SetClipboardTextAsync(text)`: `DataPackage.SetText` then `Clipboard.SetContent`.
- Get the window handle the way the service already gets its `XamlRoot` (pass the `Window` to the constructor if it only
  holds the root; update `App.xaml.cs`).
- Only one `ContentDialog` can be open at a time (already true for the other dialogs): do not open the prompt from
  inside another dialog.

## Out of scope

Callers of the new members (later cards).

## Acceptance criteria

- [x] `dotnet publish src/Yaapic.App.WinUI -c Release -r win-x64 -p:PublishAot=true`: 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: a temporary debug call is not committed; instead verify through the first
      Exercised so far: none (published exe starts and stays alive; the four members await M2-19 and M2-37).
      caller (M2-19 save/copy, M2-37 prompts) and note here which of the four members were exercised.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): implement prompt, file picker and clipboard in the dialog service`
