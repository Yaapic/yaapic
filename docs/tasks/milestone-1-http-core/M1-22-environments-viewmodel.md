# M1-22: Environments ViewModel

Status: todo
Platform: any
Depends on: M1-20

## Goal

Select the active environment and edit environments and their variables.

## Design decisions

- `partial class EnvironmentsViewModel : ObservableObject, IActiveEnvironmentProvider` in
  `Yaapic.ViewModels.Environments`, constructor `(IEnvironmentStore store, IDialogService dialogs)`.
- `partial class EnvironmentItemViewModel : ObservableObject`: `Guid Id`, observable `Name`,
  `KeyValueEditorViewModel Variables`, `IsDirty` (against the last saved definition, same approach as M1-17),
  `EnvironmentDefinition ToDefinition()`, `MarkSaved()`.
- `ObservableCollection<EnvironmentItemViewModel> Environments`; observable `ActiveEnvironment`
  (`EnvironmentItemViewModel?`, null = "No environment").
- `Current` returns `ActiveEnvironment?.ToDefinition()`: the **current edited values**, saved or not, are used for
  sending.
- `Task LoadAsync(Guid? activeId)`: loads the store; selects `activeId` if found, else null.
- `[RelayCommand] void Add()`: adds `EnvironmentDefinition.Create("New Environment")` as a dirty item and selects it
  for editing (not as active).
- observable `EditedEnvironment` (the item shown in the management view). `[RelayCommand] Task Save(EnvironmentItemViewModel item)`
  stores and marks saved; `LastError` on `StorageException`.
- `[RelayCommand] Task Delete(EnvironmentItemViewModel item)`: `ConfirmDeleteAsync`; on true calls
  `DeleteConfirmedAsync(item)`. `public Task DeleteConfirmedAsync(EnvironmentItemViewModel item)` (no prompt, for views
  that confirm inline): removes from store and list; if it was active, `ActiveEnvironment = null`; if it was edited,
  `EditedEnvironment = null`.
- `event EventHandler? ActiveEnvironmentChanged`.

## Out of scope

Globals, secret values, import/export, duplicate environment.

## Acceptance criteria

- [ ] Tests: load selects the given active id or null for an unknown id; `Current` reflects unsaved variable edits;
      add/save/delete with store checks; deleting the active environment clears it; `ActiveEnvironmentChanged` raised;
      `RequestEditorViewModel` sends with the variable of the active environment (one integration-style test with
      `FakeHttpTransport`).
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add environments view model`
