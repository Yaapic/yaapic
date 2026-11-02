# M2-54: Core: history store port and in-memory double

Status: todo
Platform: any
Depends on: M2-53

## Goal

The port for the request history, with a test double.

## Design decisions

- `IHistoryStore` in `Yaapic.Core.Storage`, all methods take a `CancellationToken` and throw `StorageException` on I/O
  failure:
  - `Task AppendAsync(HistoryEntry entry, ...)`.
  - `Task<IReadOnlyList<HistoryEntry>> ListAsync(...)`: every kept entry, **newest first** (by `At`, then by `Id`
    descending for equal times).
  - `Task DeleteAsync(Guid id, ...)`: unknown id is a no-op.
  - `Task ClearAsync(...)`.
  - A public constant `IHistoryStore.MaxEntries = 500` documents the retention (ADR 0008); implementations keep at most
    that many of the newest entries.
- `InMemoryHistoryStore` in `tests/Yaapic.TestDoubles`: same semantics (including the retention cap, applied on append),
  a settable failure for tests.

## Out of scope

The file implementation (M2-55), ViewModels.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests` for the double: append then list newest first; equal times ordered by id; retention
      drops the oldest beyond 500; delete; unknown delete; clear.
- [ ] 0 warnings.

## Suggested commit message

`feat(core): add history store port and in-memory double`
