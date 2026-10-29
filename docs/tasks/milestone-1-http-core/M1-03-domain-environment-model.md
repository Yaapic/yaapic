# M1-03: Domain environment and session model

Status: done
Platform: any
Depends on: M1-01

## Goal

The environment type that holds variables, and the session state type used by storage.

## Design decisions

- `sealed record EnvironmentDefinition(Guid Id, string Name, IReadOnlyList<KeyValueEntry> Variables)` in
  `Yaapic.Domain.Environments` (folder `src/Yaapic.Domain/Environments/`). `KeyValueEntry.Key` is the variable name,
  case-sensitive.
- `static EnvironmentDefinition Create(string name)`: new `Guid`, empty variables, trimmed name, `ArgumentException` on
  null or whitespace.
- `bool IsEquivalentTo(EnvironmentDefinition other)`, same list semantics as `HttpRequestDefinition.IsEquivalentTo`.
- In M1 only one environment is active at a time and it is the only variable scope (no globals, no collection variables).
- `sealed record SessionState(IReadOnlyList<Guid> OpenRequestIds, Guid? ActiveRequestId, Guid? ActiveEnvironmentId)` in
  `Yaapic.Domain.Workspace` (folder `src/Yaapic.Domain/Workspace/`), with `static SessionState Empty` (empty list, null ids).

## Out of scope

Secrets, initial vs current values, globals, YAML.

## Acceptance criteria

- [x] Types exist, 0 warnings.
- [x] Tests in `Yaapic.Domain.Tests`: `Create` trims and rejects blank names; `IsEquivalentTo` true for equal content,
      false after changing the name, one variable value, or the variable order; `SessionState.Empty` values.

## Suggested commit message

`feat(domain): add environment and session state model`
