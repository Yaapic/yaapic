# M2-28: Workspace index scanner

Status: todo
Platform: any
Depends on: M2-27

## Goal

One internal component that reads the `requests/` tree and answers "which folder or file holds this id", shared by the
request store and the collection store.

## Design decisions

- `internal sealed class WorkspaceIndex` in `Yaapic.Infrastructure.Storage` with `static WorkspaceIndex Scan(string
  requestsRoot)`; a missing root gives an empty index. It walks the tree once:
  - A directory containing `folder.yaml` is a folder (parsed with `FolderYamlSerializer`); a directory without it, or
    with an unparsable one, is skipped **with its whole subtree** and reported. `*.request.yaml` files in any folder or
    at the root are requests (parsed with `RequestYamlSerializer`); unparsable files are reported and skipped. Other
    files are ignored. Hidden entries (name starting with `.`) are ignored.
  - Duplicate ids (two folders, two requests, or a folder and a request): the first by ordinal path wins, the others
    are reported.
- Members (all read-only): `IReadOnlyList<IndexedFolder> Folders` (`FolderDefinition Folder`, `string DirectoryPath`,
  `Guid? ParentId`), `IReadOnlyList<IndexedRequest> Requests` (`HttpRequestDefinition Request`, `string FilePath`,
  `Guid? ParentId`), `IReadOnlyList<string> Errors` (relative paths with the reason), lookups `TryGetFolder(Guid, out)`
  and `TryGetRequest(Guid, out)`, and `IReadOnlyList<CollectionNode> BuildTree()` using `CollectionNode.Sort`.
- Helper `WorkspaceFiles.UniqueName(string parentDirectory, string slug, string suffix, string? ownPath)` (extending M1-13's
  helper class): returns `slug + suffix` or `slug-2 + suffix`, `-3`, ... until no other entry of `parentDirectory` has
  that name, ignoring the entry at `ownPath` (for renames). Case-insensitive comparison, so it also works on Windows
  and macOS.
- No caching beyond the instance: callers rescan when they need fresh data.

## Out of scope

Writing to disk, file watching.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` on a unique temp directory deleted after each test: empty and missing
      root; unfiled request; collection with nested folder and requests; tree order; directory without `folder.yaml`
      skipped and reported with its subtree; corrupt request reported; duplicate ids; hidden entries ignored;
      `UniqueName` with collisions, with `ownPath`, and case-insensitive.
- [ ] 0 warnings.

## Suggested commit message

`feat(infrastructure): add workspace index scanner`
