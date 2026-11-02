# M2-10: Dialog service: text prompt, file pickers and clipboard

Status: done
Platform: any
Depends on: M2-01

## Goal

The UI-neutral port offers the platform interactions milestone 2 needs, without breaking the two App projects.

## Design decisions

- `IDialogService` in `Yaapic.UI.Abstractions` gains four members, each with a **default interface implementation** so
  `WinUIDialogService` and `AvaloniaDialogService` keep compiling until their own cards (M2-15, M2-20) override them:
  - `Task<string?> PromptTextAsync(string title, string label, string initialText)`: returns the entered text, or null
    when canceled. Default: `Task.FromResult<string?>(null)`.
  - `Task<string?> PickFileAsync(string title)`: returns an existing file path, or null. Default: null.
  - `Task<bool> SaveFileAsync(string suggestedName, byte[] content)`: asks for a destination, writes the bytes, returns
    true when written; false when canceled or on an I/O error. Default: false.
  - `Task SetClipboardTextAsync(string text)`: default: completed task.
  Each has an XML doc comment saying the default does nothing and platforms override it.
- `FakeDialogService` (`tests/Yaapic.TestDoubles`) implements them with scripted answers and call recording:
  `Queue<string?> PromptAnswers` (empty queue = null), `List<(string Title, string Label, string Initial)> Prompts`,
  `string? PickFileAnswer`, `bool SaveFileAnswer` with `List<(string Name, byte[] Content)> SavedFiles`,
  `List<string> ClipboardTexts`.
- The architecture tests need no change (`UI.Abstractions` still references nothing).

## Out of scope

The platform implementations (M2-15, M2-20) and every caller (later ViewModel cards).

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` (or a new small test file next to the doubles' users): the fake returns scripted
      answers in order and records its calls; an `IDialogService` implementation that overrides nothing returns the
      defaults.
- [x] Whole solution builds with 0 warnings (App projects untouched).

## Suggested commit message

`feat(ui-abstractions): add text prompt, file picker and clipboard to the dialog service`
