# M2-33: Collection tree: folder commands

Status: todo
Platform: any
Depends on: M2-32, M2-10

## Goal

Create, rename and delete collections and folders from the tree ViewModel.

## Design decisions

- On `CollectionTreeViewModel` (every command catches `StorageException` into `LastError` and leaves the tree as is):
  - `[RelayCommand] Task NewCollectionAsync()`: `dialogs.PromptTextAsync("New collection", "Name", "New Collection")`; on a
    non-null answer, `SaveFolderAsync(FolderDefinition.Create(name), null)`, refresh.
  - `[RelayCommand] Task NewFolderAsync(CollectionNodeViewModel parent)`: same with title "New folder" and the given
    folder as parent; the parent is expanded afterwards. Ignored when `parent` is not a folder.
  - `[RelayCommand] Task RenameFolderAsync(CollectionNodeViewModel folder)`: prompt "Rename" with the current name; on a
    non-null answer different from the current, `SaveFolderAsync(folder.Node.Folder with { Name = name }, null)`, refresh.
  - `[RelayCommand] Task DeleteFolderAsync(CollectionNodeViewModel folder)`: `ConfirmDeleteAsync(label)` where label is
    the name when the folder is empty, else `"{name} and its {n} requests"` (n = request descendants); on true, close the
    tabs of every request below it with `tabs.CloseWithoutPrompt`, `DeleteFolderAsync`, refresh.
- A blank answer from the prompt is treated as cancel (the platform dialogs already prevent it; the ViewModel checks too).

## Out of scope

Request commands (M2-34), moving, folder settings (M2-43).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests` with `FakeDialogService` and `InMemoryCollectionStore`: new collection and nested
      folder appear after refresh with the entered name; cancel creates nothing; rename updates the name and keeps the
      children; delete confirmed removes the folder and closes the open tabs of its requests, with the label wording for
      empty and non-empty folders; delete refused changes nothing; a storage failure sets `LastError`.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add folder commands to the collection tree`
