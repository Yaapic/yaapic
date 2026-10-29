# M0-02: Core project skeletons and architecture tests

Status: done
Platform: any
Depends on: M0-01

## Goal

Create `Yaapic.Domain`, `Yaapic.Core`, `Yaapic.UI.Abstractions`, `Yaapic.ViewModels` and
`tests/Yaapic.Architecture.Tests`.

## Design decisions

- Rules: `Domain` depends on nothing; `Core` depends on `Domain`; `ViewModels` depends on `Core` and
  `UI.Abstractions` only; App projects contain no business logic.
- Architecture tests must be AOT-friendly; verify NetArchTest compatibility first and record the result in
  `docs/dependencies.md` (fallback: assembly reference checks).
- Every later card that adds a project adds its rule to these tests.

## Out of scope

Domain models, services, UI.

## Acceptance criteria

- [x] Projects build with `IsAotCompatible` and 0 warnings.
- [x] Architecture tests pass; a deliberately wrong reference makes them fail (checked once, not committed).
- [x] `dotnet test --solution Yaapic.sln -c Release` is green.

## Suggested commit message

`feat: add core project skeletons and architecture tests`
