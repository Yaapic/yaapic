# M1-13: File request store

Status: done
Platform: any
Depends on: M1-12

## Goal

`IRequestStore` on `<workspace>/requests/*.request.yaml`.

## Design decisions

- `internal static class WorkspaceFiles` in `Yaapic.Infrastructure.Storage` with:
  - `string Slugify(string name, string fallback)`: rule of `docs/file-format.md`.
  - `Task WriteAtomicAsync(string path, string content, CancellationToken ct)`: create the directory if needed, write
    UTF-8 without BOM to `path + ".tmp"`, then `File.Move(tmp, path, overwrite: true)`.
  These are reused by M1-14 and M1-15.
- `public sealed class FileRequestStore(string workspaceRoot) : IRequestStore`, directory `Path.Combine(workspaceRoot, "requests")`.
- `ListAsync`: create nothing; a missing directory returns an empty list. Read every `*.request.yaml` (top directory
  only); files that fail to parse are skipped and their file names added to `public IReadOnlyList<string> LastListErrors`
  (reset on each call). Keep an in-memory map `Id -> file path` from the last listing. Two files with the same id: keep
  the first by file name (ordinal) and report the second in `LastListErrors`. Sort as the port requires.
- `SaveAsync`: wanted file name = `Slugify(name, "request") + ".request.yaml"`. If another id already owns that name (map,
  refreshed by a listing when the map is empty), append `-2`, `-3`, ... to the slug. If this id had another file, write
  the new file first, then delete the old one (rename follows the request name). Uses `WriteAtomicAsync`.
- `DeleteAsync`: deletes the mapped file if present; unknown id is a no-op.
- I/O exceptions (`IOException`, `UnauthorizedAccessException`) are wrapped in `StorageException`.

## Out of scope

File watching, external edits while the app runs, sub-folders/collections (M2), trash/undo.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests` on a unique temp directory deleted after each test: save then list; save
      twice same id overwrites one file; rename moves the file; slug collision gets `-2`; delete; missing directory lists
      empty; a corrupt file is skipped and reported; duplicate id reported; no `.tmp` file left behind; file content has
      `\n` line endings and no BOM.
- [x] 0 warnings.

## Suggested commit message

`feat(infrastructure): add file request store`
