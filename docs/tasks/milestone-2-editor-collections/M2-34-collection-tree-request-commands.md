# M2-34: Collection tree: request commands and move

Status: todo
Platform: any
Depends on: M2-33

## Goal

New request in a folder, rename, duplicate, delete and move, for requests and folders.

## Design decisions

- On `CollectionTreeViewModel` (storage errors go to `LastError`; every command ends with a refresh):
  - `[RelayCommand] Task NewRequestAsync(CollectionNodeViewModel? parent)`: `HttpRequestDefinition.Create("New Request")`,
    `requests.SaveAsync`, `collections.MoveAsync(id, parentFolderId)` when `parent` is a folder (null or a request node
    = unfiled), expand the parent, `tabs.Open` the saved definition (the tab is not new and not dirty).
  - `[RelayCommand] Task RenameRequestAsync(CollectionNodeViewModel node)`: prompt "Rename" with the current name. If a
    tab for that request is open: when it was **clean**, set its `Name` and `tabs.SaveAsync(tab)`; when it was **dirty**,
    only set its `Name` (it stays dirty, the store is untouched, the user saves later). If no tab is open, save the
    stored definition with the new name through `requests.SaveAsync`.
  - `[RelayCommand] Task DuplicateRequestAsync(CollectionNodeViewModel node)`: copy with a new id and the name
    `"{name} copy"`, saved through `requests.SaveAsync`, moved into the same folder, opened in a tab.
  - `[RelayCommand] Task DeleteRequestAsync(CollectionNodeViewModel node)`: `ConfirmDeleteAsync(name)`; on true
    `tabs.CloseWithoutPrompt`, `requests.DeleteAsync`.
  - `IReadOnlyList<FolderOption> GetMoveTargets(CollectionNodeViewModel node)` with `sealed record FolderOption(Guid? Id,
    string Path)`: the first entry is the top level (`Id` null, path "Top level"), then every folder as `"Collection /
    Folder / ..."` in tree order; excluded: the node's current parent, and for a folder node itself and its descendants.
  - `[RelayCommand] Task MoveAsync(MoveRequest move)` with `sealed record MoveRequest(CollectionNodeViewModel Node,
    FolderOption Target)` calling `collections.MoveAsync(node.Id, target.Id)`; the target folder is expanded.
- Each command works for the node kind it is documented for and ignores the other, so views need no logic.

## Out of scope

Drag and drop, manual ordering, multi-select, copying between workspaces.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests` (in-memory doubles): new request unfiled and inside a folder, opened in a tab;
      rename of a closed, clean-open and dirty-open request; duplicate keeps content, folder and gets the new id and name;
      delete confirmed/refused with its tab closed; `GetMoveTargets` exclusions and path text; move request and folder, the
      moved folder's requests keep their ids; storage failure -> `LastError`.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add request commands and move to the collection tree`
