# M4-68: WinUI: code generation dialog

Status: todo
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

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe: open the dialog for a POST JSON request, switch through the six languages (text changes, highlighting where a grammar exists, plain text otherwise),
  Copy puts the text on the clipboard (paste it into the editor to check), edit the request and reopen to see the change, the unresolved-variable line appears for `{{missing}}`. Record
  anything not run.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the code generation dialog`
