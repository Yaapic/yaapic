# M0-01: Solution, build conventions, AOT analyzers

Status: done
Platform: any
Depends on: none

## Goal

Create `Yaapic.sln` and the shared build configuration.

## Design decisions

- `Directory.Build.props`: latest C#, nullable enabled, `TreatWarningsAsErrors`, `IsAotCompatible`,
  `EnableTrimAnalyzer`, `EnableAotAnalyzer`, `TrimmerSingleWarn=false`.
- `Directory.Packages.props` (central package management), `global.json` pinning the SDK, `.editorconfig`.
- No dependency without a written justification in `docs/dependencies.md`.

## Out of scope

Any production code, UI projects, CI.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` succeeds with 0 warnings (an empty solution is valid).
- [x] `dotnet format --verify-no-changes` passes.

## Suggested commit message

`build: add solution, shared build props and AOT analyzers`
