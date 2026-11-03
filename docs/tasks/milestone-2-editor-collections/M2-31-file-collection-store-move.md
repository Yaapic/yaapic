# M2-31: File collection store: move folders and requests

Status: done
Platform: any
Depends on: M2-30

## Goal

`MoveAsync` on disk.

## Design decisions

- Replace the `NotSupportedException` stub of M2-30. `MoveAsync(id, newParentId, ct)`: scan; the id is a folder or a request
  (else `StorageException("Unknown item")`); the target directory is `requests/` for null, else the folder's directory
  (unknown -> `StorageException`).
- Folder: throws `StorageException` when the target is the folder itself or inside it. Otherwise the new directory name is
  `WorkspaceFiles.UniqueName(targetDirectory, currentDirectoryName, "", ownPath: currentPath)` and the move is one
  `Directory.Move`. Same parent = no-op.
- Request: the new file name is `UniqueName(targetDirectory, slug-of-current-file-name-without-suffix, ".request.yaml",
  ownPath)`; a single `File.Move`. Same directory = no-op.
- The `IRequestStore` id -> path map of `FileRequestStore` is not shared: callers rescan on the next list (documented in
  the XML doc of both stores).
- I/O exceptions -> `StorageException`.

## Out of scope

Reordering inside a folder, copy.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests`: move a request into a folder and back to the root; move a folder under
      another with its requests intact; name collision at the target gets `-2`; moving a folder into itself or a
      descendant throws and changes nothing; unknown id and unknown target throw; same-parent move is a no-op;
      `FileRequestStore` lists moved requests with their new location after a scan.
- [x] 0 warnings.

## Suggested commit message

`feat(infrastructure): move requests and folders between folders`
