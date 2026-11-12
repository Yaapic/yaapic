# R-04: Velopack adapter and startup hook

Status: done
Platform: any
Depends on: R-03

## Goal

`IUpdateService` implemented on Velopack with GitHub Releases as the source, and the app startup wired for install/uninstall
hooks, all Native AOT clean.

## Design decisions

- `VelopackUpdateService` in `Yaapic.Infrastructure/Updates` (or a small `Yaapic.Updates` project if the package must not
  reach Infrastructure; architecture tests decide). Source: `GithubSource("https://github.com/Yaapic/yaapic", null, prerelease)`
  where `prerelease` follows the channel.
- `UpdateHooks.Run()` (wraps `VelopackApp.Build().Run()`) is the first statement of the Avalonia `Main` and the first line of the
  WinUI `App` constructor (WinUI generates its own `Main`, and replacing it is riskier than running the hook there); it handles the
  installer hooks and exits early when Velopack asks. Without it Velopack has no locator, so `IsSupported` treats that as not installed.
- `IsSupported` is `UpdateManager.IsInstalled`.
- No reflection: if the package needs trimming roots, they go in the project with a one-line XML comment each.

## Out of scope

UI, packaging (R-08).

## Acceptance criteria

- [x] AOT publish of both apps with the package: no new trim/AOT warnings (warnings are errors).
- [x] Integration test against a local folder feed and Velopack's `TestVelopackLocator` (no `vpk`, no install): available, up to
      date and failed (malformed feed) map correctly. A real install and update is R-08's check. A missing feed file is "up to date"
      for the folder source, like a GitHub repository without releases.
- [x] `NativeSmoke` still passes (run on Windows here; Linux and macOS are covered by CI).

## Suggested commit message

`feat: add the Velopack update service`
