# M2-29: File request store: requests inside folders

Status: todo
Platform: any
Depends on: M2-28

## Goal

`FileRequestStore` reads requests from the whole tree and keeps each request where it lives when it is saved.

## Design decisions

- `ListAsync` uses `WorkspaceIndex.Scan` (M2-28): every request in the tree, sorted as the port requires (name, then id).
  `LastListErrors` is the index error list (folder-level errors included). The id -> path map is refreshed from each scan.
- `SaveAsync`: if the id is known, the target directory is the file's current directory; if unknown, `requests/`
  (unfiled). The file name is `Slugify(name, "request") + ".request.yaml"`, made unique **within that directory** with
  `WorkspaceFiles.UniqueName`. Rename = write the new file then delete the old one, as in M1-13. Writes stay atomic.
  Rescan when the map is empty.
- `DeleteAsync`: deletes the mapped file; unknown id is a no-op. Empty folders are left alone.
- Behaviour for flat workspaces is identical to M1 (all M1 `FileRequestStoreTests` pass unchanged).
- I/O exceptions are wrapped in `StorageException` as before.

## Out of scope

Creating, renaming or moving folders (M2-30, M2-31), the sidebar.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests`: requests in nested folders are listed; saving an existing nested request
      overwrites in place and keeps its folder; renaming it renames the file inside the same folder; a name that collides
      only inside its folder gets `-2`, the same name in another folder does not; unknown id saves unfiled; delete of a
      nested request; folder errors surface in `LastListErrors`; all M1 tests unchanged and green.
- [ ] 0 warnings.

## Suggested commit message

`feat(infrastructure): list and save requests inside collection folders`
