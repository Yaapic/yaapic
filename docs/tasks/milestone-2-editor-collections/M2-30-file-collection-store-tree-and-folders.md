# M2-30: File collection store: tree and folder management

Status: done
Platform: any
Depends on: M2-26, M2-29

## Goal

`ICollectionStore` on disk: load the tree, create, update and delete folders.

## Design decisions

- `public sealed class FileCollectionStore(string workspaceRoot) : ICollectionStore` in `Yaapic.Infrastructure.Storage`,
  root directory `Path.Combine(workspaceRoot, "requests")`; reuses `WorkspaceIndex` (M2-28), `FolderYamlSerializer`
  (M2-27) and `WorkspaceFiles.WriteAtomicAsync`.
- `LoadTreeAsync`: `WorkspaceIndex.Scan(...).BuildTree()`; `public IReadOnlyList<string> LastLoadErrors` (reset per
  call) holds the index errors.
- `SaveFolderAsync(folder, parentId)`: scan; for an unknown id, resolve the parent directory (`requests/` for null;
  unknown `parentId` -> `StorageException`), create `<slug>` (unique among siblings, fallback `folder`) and write
  `folder.yaml` atomically. For a known id: rewrite its `folder.yaml`; when the name changed, rename the directory to
  the new unique slug (`Directory.Move`) and write the file there. A folder that is a descendant keeps working because
  paths are re-resolved on the next scan.
- `DeleteFolderAsync`: recursive `Directory.Delete` of the folder directory; unknown id is a no-op.
- `MoveAsync` is implemented by M2-31; in this card it throws `NotSupportedException("Implemented by M2-31")` (a
  one-line stub, not tested here).
- I/O exceptions (`IOException`, `UnauthorizedAccessException`) -> `StorageException`.

## Out of scope

Moving (M2-31), file watching, trash/undo.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests` on a temp directory: empty tree; create top-level and nested folder (files and
      names on disk, `folder.yaml` content); tree contains unfiled and nested requests with order; rename renames the
      directory and keeps the requests inside; slug collision gives `-2`; update variables and description in place;
      delete removes nested folders and request files; unknown ids; unknown parent throws; `.tmp` files never left.
- [x] 0 warnings.

## Suggested commit message

`feat(infrastructure): add file collection store for folders`
