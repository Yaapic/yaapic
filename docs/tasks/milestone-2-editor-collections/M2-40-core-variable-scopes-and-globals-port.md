# M2-40: Core: variable scopes, sender and globals port

Status: done
Platform: any
Depends on: M2-01

## Goal

A request is resolved against four scopes (environment, collection, globals, dynamic) in the precedence of ADR 0008.

## Design decisions

- In `Yaapic.Core.Variables`:
  - `enum VariableScopeKind { Environment, Collection, Globals, Dynamic }`.
  - `public sealed record VariableScopes(EnvironmentDefinition? Environment, IReadOnlyList<KeyValueEntry> Collection,
    IReadOnlyList<KeyValueEntry> Globals)` with `static VariableScopes None` (null, `[]`, `[]`),
    `IVariableSource CreateSource()` = `CompositeVariableSource(environment, collection, globals, dynamic)` in that
    order, and `bool TryResolve(string name, out string value, out VariableScopeKind scope)` using the same order (the
    collection and globals entries are wrapped in `EnvironmentVariableSource` over a temporary `EnvironmentDefinition`, so
    the rules are the existing ones: enabled entries only, non-empty key, case-sensitive, last entry wins). For a dynamic
    variable `value` is the generated value and `scope` is `Dynamic`.
- `RequestSender` gains `Task<SendReport> SendAsync(HttpRequestDefinition request, VariableScopes scopes,
  CancellationToken ct)`; the existing `(request, environment, ct)` overload delegates with
  `new VariableScopes(environment, [], [])`. Behaviour of M1 sends does not change.
- `Yaapic.Core.Storage.IGlobalsStore`: `Task<IReadOnlyList<KeyValueEntry>> LoadAsync(CancellationToken ct)` (missing =
  empty) and `Task SaveAsync(IReadOnlyList<KeyValueEntry> variables, CancellationToken ct)`; both throw
  `StorageException` on I/O failure. `InMemoryGlobalsStore` in `tests/Yaapic.TestDoubles` (same pattern as the other
  doubles, with a settable failure for tests).

## Out of scope

Persistence (M2-41), ViewModels (M2-42, M2-44).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: precedence (same name in all four scopes resolves to the environment value, then
      collection, then globals; a name only in globals resolves from globals); disabled and empty-key entries ignored;
      `TryResolve` reports the right scope for each, `Dynamic` for `{{$guid}}`, false for unknown; `SendAsync` with scopes
      substitutes from each scope through `FakeHttpTransport`; the old overload still works; unresolved names still reported.
- [x] 0 warnings.

## Suggested commit message

`feat(core): resolve variables across environment, collection and globals scopes`
