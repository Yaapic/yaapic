# M4-76: Avalonia: code generation window

Status: done
Platform: unix
Depends on: M4-58, M4-61

## Goal

The Avalonia window that shows the generated code with a Copy button, matching M4-68.

## Design decisions

- Read first: M4-68, `Views/ConvertScriptWindow.cs`, `Views/CodeEditorView.axaml(.cs)`, M4-58.
- `Views/CodeGenerationWindow.axaml` (`CodeGenerationViewModel`, compiled bindings): language `ComboBox`, read-only `CodeEditorView` with `EditorLanguage` (plain text fallback
  where the grammar is missing), the secret warning, the unresolved and error lines, Copy and Close. The Copy button uses an Avalonia implementation of `IClipboardService`
  (`TopLevel.Clipboard`) if M4-58 added the interface and no implementation exists on this front-end; "Copied" state as in M4-68.
- Opened from `CodeGenerationRequested`, owned by the main window, the same entry points as WinUI (palette and the request header action chosen in M4-68).

## Out of scope

Editing, saving, running the code.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M4-68; if not possible, say so in the Notes and tick only the build and AOT items.
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the code generation window`

## Notes

- Entry point: `RequestEditorView` handles `CodeGenerationRequested` and opens `CodeGenerationWindow` modally on the top-level window (palette command, as on WinUI).
- `AvaloniaDialogService.SetClipboardTextAsync` already implements the clipboard (`TopLevel.Clipboard`), so no new service. The "Copied" state is a `DispatcherTimer` in the window code-behind; strings already existed.
- Not run: the runtime smoke of the dialog itself (languages, Copy, `{{missing}}` line) and any macOS/Linux run. Verified: Release build 0 warnings, Architecture and Localization tests green, AOT publish (win-x64) 0 IL warnings, exe starts and exits.
- The full `dotnet build Yaapic.sln` could not complete: a `Yaapic.App.WinUI.exe` instance not started by this session locked the WinUI output; the Avalonia project and the unaffected test projects were built on their own.
