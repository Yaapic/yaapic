# M1-11: Storage ports and shared test doubles

Status: todo
Platform: any
Depends on: M1-03, M1-07

## Goal

Core ports for persisting requests, environments and session state, plus in-memory doubles shared by later test
projects.

## Design decisions

- Namespace `Yaapic.Core.Storage`, folder `src/Yaapic.Core/Storage/`:
  - `interface IRequestStore { Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct);
    Task SaveAsync(HttpRequestDefinition request, CancellationToken ct); Task DeleteAsync(Guid id, CancellationToken ct); }`
    `ListAsync` returns requests sorted by `Name` (`StringComparer.OrdinalIgnoreCase`), then by `Id`. `SaveAsync`
    inserts or replaces by `Id`. `DeleteAsync` of an unknown id is a no-op.
  - `interface IEnvironmentStore`: same three members with `EnvironmentDefinition`.
  - `interface ISessionStore { Task<SessionState> LoadAsync(CancellationToken ct); Task SaveAsync(SessionState state, CancellationToken ct); }`.
    `LoadAsync` never throws for a missing or unreadable file: it returns `SessionState.Empty`.
  - `sealed class StorageException(string message, Exception? inner = null) : Exception` for I/O and format errors.
- New project `tests/Yaapic.TestDoubles` (class library, `net10.0`, `IsAotCompatible` false, `IsPackable` false,
  references `Yaapic.Core` and `Yaapic.Domain`, added to `Yaapic.sln`, not a test runner):
  `InMemoryRequestStore`, `InMemoryEnvironmentStore`, `InMemorySessionStore` (implementing the ports with the sorting
  rule above, plus a public `SaveCount` counter each) and `FakeHttpTransport` (constructor takes a
  `Func<ResolvedRequest, CancellationToken, Task<SendResult>>`, records `Requests`).
- `Yaapic.Core.Tests` references `Yaapic.TestDoubles` and the M1-07 local fake is replaced by `FakeHttpTransport`.

## Out of scope

File implementations (M1-12 to M1-15), file watching, conflict detection.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests` for the in-memory doubles (sorting, replace by id, delete no-op) so later cards can
      trust them.
- [ ] 0 warnings, architecture tests green (test projects are not under `src/`).

## Suggested commit message

`feat(core): add storage ports and shared test doubles`
