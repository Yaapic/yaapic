# M1-20: Tabs ViewModel, save and dialog service

Status: todo
Platform: any
Depends on: M1-19

## Goal

Open, switch, save and close request tabs, with an unsaved-changes prompt behind a UI-neutral dialog port.

## Design decisions

- In `Yaapic.UI.Abstractions` (namespace `Yaapic.UI.Abstractions`): `enum UnsavedChoice { Save, Discard, Cancel }` and
  `interface IDialogService { Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName); Task<bool> ConfirmDeleteAsync(string itemName); }`.
  Add `FakeDialogService` (scripted answers, records calls) to `Yaapic.TestDoubles` (add the `Yaapic.UI.Abstractions`
  reference there).
- `partial class TabsViewModel : ObservableObject` in `Yaapic.ViewModels.Tabs`, constructor
  `(IRequestStore store, IDialogService dialogs, Func<HttpRequestDefinition, bool, RequestEditorViewModel> createEditor)`
  (the factory receives the definition and `isNew`; the composition root supplies sender and environment).
- `ObservableCollection<RequestEditorViewModel> Tabs`, observable `ActiveTab` (null when no tab).
- `NewTab()`: creates `HttpRequestDefinition.Create("New Request")` as new, appends, activates.
- `Open(HttpRequestDefinition definition)`: if a tab with that `Id` exists, activate it; otherwise append and activate.
- `Task<bool> SaveAsync(RequestEditorViewModel tab)`: `store.SaveAsync(tab.ToDefinition())`, `tab.MarkSaved()`, raise
  `event EventHandler<HttpRequestDefinition>? RequestSaved`, return true. On `StorageException` return false and set
  observable `LastError` to its message. `SaveActiveCommand` saves `ActiveTab`.
- `Task<bool> CloseAsync(RequestEditorViewModel tab)`: if not dirty, close. If dirty, ask
  `ConfirmUnsavedAsync(tab.Name)`: `Save` -> save then close (keep open if save failed); `Discard` -> close;
  `Cancel` -> keep, return false. A new tab that was never edited (`IsNew` and equivalent to its creation state) closes
  without asking. When the active tab closes, activate the tab now at the same index, else the previous one, else null.
  `CloseActiveCommand` closes `ActiveTab`.
- `void CloseWithoutPrompt(Guid id)`: used when a request is deleted (M1-21).
- `event EventHandler? SessionChanged`: raised when tabs are added, removed, or `ActiveTab` changes (M1-23 persists it).

## Out of scope

Tab reordering, pinning, duplicate, close-others, restoring unsaved content.

## Acceptance criteria

- [ ] Tests (in-memory store + fake dialogs): new tab activated; opening an already open id activates it without
      duplicating; save writes to the store, clears dirty and raises `RequestSaved`; save failure sets `LastError`;
      close clean tab without prompt; untouched new tab closes without prompt; dirty close with Save/Discard/Cancel;
      active-tab selection after close (middle, last, only); `SessionChanged` raised.
- [ ] 0 warnings, architecture tests green.

## Suggested commit message

`feat(viewmodels): add tabs view model with save and close prompts`
