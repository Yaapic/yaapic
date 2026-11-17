# M4-68: WinUI: code generation dialog

Status: done
Platform: windows
Depends on: M4-58, M4-61

## Goal

A dialog that shows the generated code of the current request in the chosen language with a Copy button.

## Design decisions

- Read first: `Views/ConvertScriptDialog.cs`, `Views/CodeEditorView.xaml(.cs)` (read-only mode and `EditorLanguage`; ADR 0015 records which grammars exist), M4-58, M4-60 and how the request
  editor view exposes its commands (a "Code" button next to Send is **not** added: the entry is the palette and a small `</>` icon button in the request header if the existing header has
  icon buttons; follow the pattern already used for other secondary actions and state the choice in the Notes).
- `Views/CodeGenerationDialog.xaml` (`CodeGenerationViewModel`): a language `ComboBox` (`Targets`, `SelectedTarget`), a read-only `CodeEditorView` bound to `Code` with `EditorLanguage`
  (a language without a LoomEditor grammar falls back to `text`: the existing view already does this: verify), the secret warning line (`SecretWarning`, warning style), the unresolved
  variables line, the error line, a **Copy** button (`CopyCommand`, shows "Copied" for 2 seconds through a brief visual state, no timers in the view model) and Close.
- Size: 720 x 520 default, resizable if the existing dialogs are; the editor fills the remaining height.
- Implements the clipboard service for WinUI (`Windows.ApplicationModel.DataTransfer.Clipboard`) if M4-58 added `IClipboardService` and no implementation exists yet.
- Handles `CodeGenerationRequested` (M4-58) on the active request editor.

## Out of scope

Editing the code, saving to a file, running it.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe: open the dialog for a POST JSON request, switch through the six languages (text changes, highlighting where a grammar exists, plain text otherwise),
  Copy puts the text on the clipboard (paste it into the editor to check), edit the request and reopen to see the change, the unresolved-variable line appears for `{{missing}}`. Record
  anything not run.
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the code generation dialog`

## Notes

- Entry point: the palette command "Generate code for this request" (M4-60); no header icon button was added (the request header has none for secondary actions). `RequestEditorView` handles `CodeGenerationRequested`.
- The clipboard service already existed (`WinUIDialogService.SetClipboardTextAsync`); the "Copied" state is a `DispatcherTimer` in the dialog code-behind. Added the `codeGeneration.copied` string (EN/FR).
- Not run: the manual smoke on the published exe (dialog interaction, six languages, Copy, `{{missing}}` line). Only the AOT publish (0 IL warnings) and a clean start/exit were checked.
- Fix after the first manual look (`fix(winui): fit the code generation dialog and highlight the code`): the dialog now raises `ContentDialogMaxWidth` to 760 so the 680 px body is no longer clipped (Copy, Close and the secret warning are visible), and `CodeEditorView` maps `csharp` and `python` to LoomEditor grammars (JavaScript already worked). LoomEditor 0.6.0 ships no shell, PowerShell or Go grammar, so cURL, PowerShell and Go stay plain text. Checked on the JIT Debug build for cURL, JavaScript, Python and C#, switching language live.
- AOT crash fixed (`fix(winui): bind the code targets and JWT claims through ListMap`): `ItemsSource` was bound to `IReadOnlyList<CodeTarget>`, which CsWinRT cannot marshal as a bindable vector under Native AOT once it is not empty (the stowed XAML exception 0xc000027b, see `ListMap`). It now goes through `ListMap.Targets`. The JWT decoder dialog had the same defect on `IReadOnlyList<JwtClaimRow>` (crash on open) and now uses `ListMap.Claims`. Checked on the published AOT exe: the dialog opens from the palette, switching language works (Python shown), and the JWT (with `iat`/`exp`), certificates, Timing and Diff views open. Not checked: every language, Copy, the `{{missing}}` line. The JWT dialog is taller than a 900 px window (the claims list is below the fold).
