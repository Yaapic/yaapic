# M2-43: Folder and collection settings ViewModel

Status: todo
Platform: any
Depends on: M2-34

## Goal

Edit a folder's name and description, and a collection's variables.

## Design decisions

- `partial class FolderSettingsViewModel : ObservableObject` in `Yaapic.ViewModels.Collections`, constructor
  `(FolderDefinition folder, bool isCollection, ICollectionStore store)`: observable `Name`, `Description`;
  `KeyValueEditorViewModel Variables`; `bool ShowVariables` = `isCollection` (only top-level folders use variables,
  ADR 0008); `bool IsDirty`; `string? LastError`; `event EventHandler? Saved`; `[RelayCommand] Task SaveAsync()` calls
  `store.SaveFolderAsync(definition, null)` where the definition keeps the folder id, with the trimmed name (a blank name
  keeps the old one) and, for non-collections, the original variables untouched; on success updates the baseline and
  raises `Saved`; `StorageException` -> `LastError`.
- `CollectionTreeViewModel` gains observable `FolderSettingsViewModel? SettingsEditor`, `[RelayCommand] void
  EditSettings(CollectionNodeViewModel folder)` (ignored for requests; sets `SettingsEditor`), `[RelayCommand] void
  CloseSettings()` (null), and refreshes the tree when the editor raises `Saved`.
- `CollectionTreeViewModel.FindCollectionVariables(Guid requestId)`: the `Variables` of the top-level folder that contains
  the request (searching the loaded tree), or an empty list for an unfiled or unknown request. Used by M2-44. Only saved
  variables apply (documented in the XML doc).

## Out of scope

Views (M2-48 WinUI, M2-51 Avalonia), collection-level auth, headers or scripts (later milestones).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: editing name, description or a variable makes it dirty, save writes through the
      store and the tree shows the new name, `Saved` is raised, blank name keeps the old one, nested folder keeps its
      variables, `ShowVariables` false for non-collections, `EditSettings`/`CloseSettings` set and clear
      `SettingsEditor`, `FindCollectionVariables` for a request in a nested folder, an unfiled one and an unknown id,
      storage failure -> `LastError`.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add folder and collection settings view model`
