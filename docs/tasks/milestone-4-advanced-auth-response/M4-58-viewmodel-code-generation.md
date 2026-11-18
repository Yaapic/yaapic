# M4-58: ViewModel: code generation dialog

Status: done
Platform: any
Depends on: M4-51, M4-52, M4-53, M4-54, M4-56, M4-57

## Goal

A view model produces generated code for the current request in the chosen language, ready to show and copy.

## Design decisions

- Read first: ADR 0015, M4-50, `RequestEditorViewModel.cs` (how a send resolves the request: `IVariableScopeProvider`, `RequestResolver.Resolve`), `IPreferencesStore` and how
  another VM stores a last choice (the layout toggle of U-01 or the update channel), the clipboard abstraction used by existing "Copy" commands (grep `Clipboard` in
  `Yaapic.ViewModels` and `UI.Abstractions`; add `IClipboardService` to `UI.Abstractions` **only** if none exists).
- `CodeGenerationViewModel` in `Yaapic.ViewModels.Requests`, constructor `(CodeGeneratorRegistry registry, Func<ResolvedRequest> resolve, IPreferencesStore
  preferences, IClipboardService clipboard)`:
  - `Targets` (id, display name) from the registry; `SelectedTarget` (restored from the preference `codegen.target`, default `curl`; saved on change);
  - `Code` (the generated text, recomputed on target change and on `Refresh()`), `EditorLanguage` (from the generator), `string? Error` (a generator exception
    becomes `Strings.CodeGenerationFailed(message)` and `Code` is empty);
  - `SecretWarning` = `Strings.CodeGenerationSecretWarning` constant text (ADR 0015: the generated text may contain secrets), `UnresolvedWarning` listing unresolved
    variables (`Strings.CodeGenerationUnresolved(names)`) when any;
  - `CopyCommand` (puts `Code` on the clipboard), disabled when `Code` is empty.
- `RequestEditorViewModel` gets `CodeGenerationViewModel CreateCodeGeneration()` (a factory method: the shared registry and services come from an optional
  `CodeGenerationServices` record passed through the factories like `ScriptSendServices`; null means the feature is absent and the command is disabled) and
  `GenerateCodeCommand` raising an event `CodeGenerationRequested` (the shell opens the dialog, M4-61 and the UI cards).
  The resolve function uses the **current unsaved editor state** (not the file), with the same scopes as a send; it does not run scripts and does not call
  authenticators (ADR 0015).
- Strings (EN and FR): the five keys above plus `CodeGenerationTitle`, `CodeGenerationLanguage`, `CodeGenerationCopy`.

## Out of scope

Dialog views (M4-68, M4-76), palette (M4-60).

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` with fake generators, an in-memory preferences store and a fake clipboard: targets listed; default and restored target;
  selecting a target regenerates and persists; `Refresh` picks up editor changes; generator exception handled; unresolved variables warning; copy; the
  request editor factory with and without the services; resolution uses unsaved edits.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the code generation view model`
