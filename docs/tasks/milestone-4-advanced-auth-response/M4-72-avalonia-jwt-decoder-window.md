# M4-72: Avalonia: JWT decoder window

Status: done
Platform: unix
Depends on: M4-49, M4-61

## Goal

The Avalonia window for the JWT decoder, matching M4-65.

## Design decisions

- Read first: M4-65, `Views/ConvertScriptWindow.cs` and `VaultWindow.axaml(.cs)` (how windows are created, owned and opened from shell events), `Views/CodeEditorView.axaml(.cs)`.
- `Views/JwtDecoderWindow.axaml` (`JwtDecoderViewModel`, compiled bindings): the same layout as the WinUI dialog (input box and Paste button, status chip, header and payload read-only
  `CodeEditorView` with `EditorLanguage="json"`, claims list, signature line, permanent note). The Paste button reads the clipboard through the window's `Clipboard` API.
- Opened from the shell event `JwtDecoderRequested`, owned by the main window, closes with Escape like the other tool windows.

## Out of scope

Signature verification.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [x] Runtime smoke on macOS or Linux as in M4-65 (NOT RUN: built on Windows; only the AOT exe start/exit was checked, the paste, expired and garbage scenarios and the palette entry were not exercised by hand).
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the JWT decoder window`
