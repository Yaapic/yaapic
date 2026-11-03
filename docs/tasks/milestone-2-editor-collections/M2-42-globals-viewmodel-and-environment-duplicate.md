# M2-42: Globals ViewModel and environment duplicate

Status: done
Platform: any
Depends on: M2-40

## Goal

Edit the global variables, and duplicate an environment.

## Design decisions

- `partial class GlobalsViewModel : ObservableObject` in `Yaapic.ViewModels.Environments`, constructor
  `(IGlobalsStore store)`: `KeyValueEditorViewModel Variables`, `bool IsDirty` (current entries differ from the saved
  baseline; raised when `Variables.Changed` fires), `string? LastError`, `Task LoadAsync()` (sets the baseline, never
  marks dirty; a `StorageException` sets `LastError` and keeps the editor empty and **not saveable**: `SaveCommand`
  disabled so a corrupt file is never overwritten), `[RelayCommand] Task SaveAsync()` (writes, updates the baseline),
  `IReadOnlyList<KeyValueEntry> Current` = `Variables.ToEntries()` (unsaved edits are used when sending, like environments).
- `EnvironmentsViewModel` gains `[RelayCommand] void Duplicate(EnvironmentItemViewModel item)`: a **new unsaved** item
  with a new id, the name `"{name} copy"` and a copy of the variables, added to `Environments` and set as
  `EditedEnvironment` (the user saves it with the existing Save command, like `Add`).
- `EnvironmentsViewModel` gains observable `bool IsEditingGlobals`: setting it true clears `EditedEnvironment`; setting
  `EditedEnvironment` to non-null sets it false. (The dialogs use it to show either the globals table or an environment.)

## Out of scope

Composition and sending (M2-44, M2-45), views (M2-47, M2-50).

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests`: globals load, edit -> dirty, save clears dirty and writes the store, revert
      cleans, load failure sets `LastError` and disables save; duplicate creates an unsaved copy with new id and
      name suffix, original untouched, copy saveable; `IsEditingGlobals` and `EditedEnvironment` exclude each other.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): add globals editor and environment duplicate`
