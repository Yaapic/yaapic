# M2-26: Core: collection store port and in-memory double

Status: done
Platform: any
Depends on: M2-25

## Goal

The port that loads the tree and manages folders and placement, with a test double for the ViewModels.

## Design decisions

- `ICollectionStore` in `Yaapic.Core.Storage`:
  - `Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct)`: roots sorted with `CollectionNode.Sort`;
    roots are the top-level folders and the unfiled requests.
  - `Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct)`: creates the folder under
    `parentId` (null = top level) when its id is unknown; otherwise updates name, description and variables **in place**
    (`parentId` is ignored for an existing folder; use `MoveAsync` to relocate it).
  - `Task DeleteFolderAsync(Guid id, CancellationToken ct)`: deletes the folder and everything below it, requests
    included; unknown id is a no-op.
  - `Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct)`: moves a folder or a request (`id` may be either)
    under `newParentId` (null = root / unfiled). Throws `StorageException` when the id or the target folder is unknown,
    or when a folder would move into itself or one of its descendants. Moving to the current parent is a no-op.
  - All throw `StorageException` when the store cannot be read or written. XML docs on every member.
  - Request content is still saved and deleted through `IRequestStore`; a request saved with an unknown id lands unfiled,
    a known id stays where it is (documented on `IRequestStore.SaveAsync` in this card).
- `InMemoryCollectionStore(InMemoryRequestStore requests)` in `tests/Yaapic.TestDoubles`: keeps folders and a
  `parent id` map; builds the tree from both; same semantics and exceptions as above; deleting a folder deletes the
  requests below it from the request store.

## Out of scope

The file implementation (M2-30, M2-31), view models.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` (next to `InMemoryStoreTests`) for the double: empty tree; create top-level and nested
      folder; update keeps location; unfiled request appears at the root; move request and folder; move into own
      descendant throws; unknown ids; delete folder removes nested folders and requests; sort order.
- [x] 0 warnings; the architecture rule test needs no change.

## Suggested commit message

`feat(core): add collection store port and in-memory double`
