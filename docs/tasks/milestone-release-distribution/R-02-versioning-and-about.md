# R-02: Versioning from the tag and the About information

Status: todo
Platform: any
Depends on: R-01

## Goal

One source of truth for the app version, readable at runtime, defaulting to `0.0.0-dev` for local builds.

## Design decisions

- `Directory.Build.props` reads `YaapicVersion` (passed by the release workflow from the tag) into `Version`,
  `InformationalVersion` and `AssemblyVersion`; default `0.0.0-dev`.
- `Yaapic.Core` exposes `AppVersion` (a record around `System.Version` plus the pre-release suffix, `Parse`, comparison
  following SemVer ordering) and an `IAppInfo` port (`Version`, `Channel` derived from the suffix). The composition root
  implements it from the entry assembly; no reflection beyond `Assembly.GetName()` (AOT-safe).
- The shell shows the version in the About dialog and in the window title tooltip.

## Out of scope

The release workflow (R-08), update logic.

## Acceptance criteria

- [ ] `dotnet build -p:YaapicVersion=1.2.3-preview.4` yields that version at runtime.
- [ ] `AppVersion` ordering tests: `1.0.0-preview.1 < 1.0.0 < 1.0.1`, numeric (not text) comparison of `preview.10`.
- [ ] The About dialog on both apps shows the version.

## Suggested commit message

`feat: expose the app version and channel`
