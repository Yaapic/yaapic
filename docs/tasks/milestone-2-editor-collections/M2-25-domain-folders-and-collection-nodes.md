# M2-25: Domain: folders and collection nodes

Status: todo
Platform: any
Depends on: M2-01

## Goal

The domain model of the collection tree (ADR 0008).

## Design decisions

- In `Yaapic.Domain.Collections` (new folder `src/Yaapic.Domain/Collections/`):
  - `public sealed record FolderDefinition(Guid Id, string Name, string Description, IReadOnlyList<KeyValueEntry>
    Variables)` with `static FolderDefinition Create(string name)` (trimmed, new id, empty description and variables;
    `ArgumentException` for a blank name) and `bool IsEquivalentTo(FolderDefinition other)` (variables by sequence).
  - `enum CollectionNodeKind { Folder, Request }`.
  - `public sealed record CollectionNode` with `Guid Id`, `CollectionNodeKind Kind`, `string Name`,
    `FolderDefinition? Folder` (set for folders), `HttpRequestDefinition? Request` (set for requests),
    `IReadOnlyList<CollectionNode> Children` (always empty for requests). Factories
    `ForFolder(FolderDefinition folder, IReadOnlyList<CollectionNode> children)` and
    `ForRequest(HttpRequestDefinition request)`; constructor private so a node is always consistent.
  - `static IReadOnlyList<CollectionNode> Sort(IEnumerable<CollectionNode> nodes)`: folders first, then requests, each by
    name (ordinal, ignoring case) then by id; the single place that defines the order of ADR 0008.
  - `IEnumerable<CollectionNode> Descendants()` (depth first, not including itself) and a static
    `IEnumerable<CollectionNode> Flatten(IEnumerable<CollectionNode> roots)`.
- Keep all new types free of YAML or UI concerns.

## Out of scope

Storage (M2-26 onwards), view models.

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests`: `Create` trims and rejects blank; `Sort` puts folders first, orders ignoring case,
      breaks ties by id; factories set the right members and reject inconsistent input; `Descendants`/`Flatten` order;
      `IsEquivalentTo` with variables.
- [ ] 0 warnings.

## Suggested commit message

`feat(domain): add folder definitions and collection nodes`
