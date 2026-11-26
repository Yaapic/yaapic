# M6-15: Core: run variable state and scope source port

Status: done
Platform: any
Depends on: M6-06, M6-14

## Goal

Keep the variable changes of a run in memory so that later requests see them, whether or not they are persisted (ADR 0018,
"Variables"), and define the port through which the runner reads and persists scopes.

## Design decisions

- Read first: ADR 0018, `src/Yaapic.Core/Variables/VariableScopes.cs`, `src/Yaapic.Core/Scripting/VariableChange.cs`,
  `src/Yaapic.ViewModels/Requests/IVariableScopeProvider.cs` (what the adapter of M6-21 will wrap).
- `src/Yaapic.Core/Runner/IRunScopeSource.cs`:
  ```csharp
  public interface IRunScopeSource
  {
      VariableScopes GetScopes(Guid requestId);
      IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId);
      Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct);
  }
  ```
  (XML docs: `PersistAsync` returns one error message per scope that could not be saved.)
- `src/Yaapic.Core/Runner/RunVariableState.cs` (public sealed class, not thread-safe; one per run, one per virtual user in load tests):
  - `void Apply(IReadOnlyList<VariableChange> changes)`: records, per scope (`Environment`, `Collection`, `Globals`), the last value or
    the unset of each name, in order. Other scopes are ignored.
  - `VariableScopes Overlay(VariableScopes scopes, IterationDataRow row)`: returns a copy where each recorded change is applied to the
    matching list (set: update the enabled row of that name or append an enabled row; unset: remove every row of that name; the
    environment is rebuilt with `with { Variables = ... }` and stays `null` when there is none, in which case environment changes are
    dropped) and `Data = row.Values`. Secret flags of existing rows are kept.
  - `bool IsEmpty`.

## Out of scope

The runner loop (M6-16), the view-model adapter (M6-21).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Runner/RunVariableStateTests.cs`: set then unset then set again; changes to each scope; environment
  absent; existing secret row updated keeps `IsSecret`; disabled row of the same name gets a new enabled value; `Local` changes ignored;
  `Data` taken from the row; overlay does not mutate its input.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): keep the variable changes of a run`
