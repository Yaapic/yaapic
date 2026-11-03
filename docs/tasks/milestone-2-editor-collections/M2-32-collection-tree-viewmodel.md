# M2-32: Collection tree ViewModel

Status: done
Platform: any
Depends on: M2-26

## Goal

The sidebar tree of collections, folders and requests: load, expand, filter, open.

## Design decisions

- `partial class CollectionTreeViewModel : ObservableObject` in `Yaapic.ViewModels.Collections`, constructor
  `(ICollectionStore collections, IRequestStore requests, TabsViewModel tabs, IDialogService dialogs)` (the last two are
  used by M2-33 and M2-34; store them now).
- `partial class CollectionNodeViewModel : ObservableObject` in the same namespace: `Guid Id`, `CollectionNodeKind Kind`,
  `bool IsFolder`, `bool IsCollection` (a folder with no parent), `string Name`, `string MethodText` and `Method` (requests
  only), `string Url`, the `CollectionNode Node`, `CollectionNodeViewModel? Parent`, `ObservableCollection<
  CollectionNodeViewModel> Children`, observable `bool IsExpanded`.
- `ObservableCollection<CollectionNodeViewModel> Roots` (the filtered view), observable `string FilterText`,
  `bool IsEmpty` (no node at all in the store, used for the empty state), `string? LastError` (observable).
- `Task RefreshAsync()`: `collections.LoadTreeAsync`; rebuild the node view models; **keep `IsExpanded` of folders that
  still exist (by id)** in a private set; apply the filter; `StorageException` -> `LastError`, tree unchanged.
- Filter: trimmed, ordinal-ignore-case `Contains` on a request's `Name` or `Url` and on a folder's `Name`. A node is
  shown when it matches or has a matching descendant; a matching folder shows all its children; while the filter is
  non-empty, folders with a shown descendant are expanded (without touching the remembered expansion set); clearing the
  filter restores it. Store order is kept.
- Subscribes to `tabs.RequestSaved` and calls `RefreshAsync`.
- `[RelayCommand] void Open(CollectionNodeViewModel node)`: for a request node, `tabs.Open(node.Node.Request!)`; for a
  folder node, toggles `IsExpanded`.

## Out of scope

Folder and request commands (M2-33, M2-34), shell wiring (M2-35), persisting expansion across runs.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` with `InMemoryCollectionStore`: refresh builds the tree in store order with
      parents set; `IsCollection` only for top-level folders; expansion survives a refresh and a removed folder is forgotten;
      filter by request name, URL, folder name and nested match, case-insensitive; clearing the filter; saving a tab refreshes
      the tree; opening a request activates its tab, opening a folder toggles it; a failing store sets `LastError`.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): add collection tree view model`
