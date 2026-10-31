# M1-05: Dynamic variables

Status: done
Platform: any
Depends on: M1-04

## Goal

Built-in `{{$guid}}`-style variables as an `IVariableSource`.

## Design decisions

- `sealed class DynamicVariableSource : IVariableSource` in `Yaapic.Core.Variables`, constructors
  `DynamicVariableSource(TimeProvider time, Func<Guid> newGuid, Random random)` and `DynamicVariableSource()` (uses
  `TimeProvider.System`, `Guid.NewGuid`, `Random.Shared`).
- Supported names, exact and case-sensitive:
  - `$guid`: `newGuid().ToString("D")` (lowercase).
  - `$timestamp`: Unix time in seconds, invariant decimal string.
  - `$isoTimestamp`: UTC, format `yyyy-MM-ddTHH:mm:ss.fffZ` (literal `T` and `Z`), invariant culture.
  - `$randomInt`: `random.Next(0, 1001)`, so 0 to 1000 inclusive.
  Any other name returns false.
- Every lookup produces a fresh value: two `{{$guid}}` in one string differ.
- Precedence is the caller's job (M1-07 asks the environment first, then this source).

## Out of scope

Faker-style generators, user-defined dynamic variables, scripting.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` with a hand-written `TimeProvider` subclass returning a fixed `GetUtcNow()` (no extra
      package) and a seeded `Random`: format of each variable; two `$guid` lookups differ; `$randomInt` within 0..1000;
      unknown `$name` and wrong case return false; `VariableResolver` resolves `{{$guid}}` through this source.
- [x] 0 warnings.

## Suggested commit message

`feat(core): add dynamic variables`
