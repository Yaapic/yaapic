# R-03: Update port, domain model and settings

Status: done
Platform: any
Depends on: R-02

## Goal

The Core side of updates, with no Velopack reference: a port, the states, the user settings, and a fake for tests.

## Design decisions

- `IUpdateService` in `Yaapic.Core/Updates`: `CheckAsync(channel, ct)` returns `UpdateCheckResult` (`UpToDate`,
  `Available(AppVersion, notes)`, `Failed(reason)`), `DownloadAsync(update, IProgress<int>, ct)`, `ApplyOnRestart()` and
  `RestartAndApply()`. `IsSupported` is false for a build that is not installed (dev runs, portable copy).
- Settings are per user, not per workspace (the session store lives in the workspace), so `IUpdateSettingsStore` is a new port with a
  file store in `<user data dir>/Yaapic/updates.json` (source-generated JSON, same directory rule as the vault): `UpdateChannel` (default from the running version), `CheckAutomatically`
  (default true), `LastCheckUtc`. The "at most once a day" rule lives in a Core `UpdateScheduler` taking `TimeProvider`.
- A `FakeUpdateService` in the shared test doubles.

## Out of scope

The Velopack adapter, UI.

## Acceptance criteria

- [x] Scheduler tests with a fake clock: first start checks, second within 24 h does not, after 24 h does, disabled never does.
- [x] Settings round trip through the file store; unknown channel falls back to the running version's channel.
- [x] Architecture tests still pass (Core has no new package reference).

## Suggested commit message

`feat: add the update port, scheduler and settings`
