# R-04: Velopack adapter and startup hook

Status: todo
Platform: any
Depends on: R-03

## Goal

`IUpdateService` implemented on Velopack with GitHub Releases as the source, and the app startup wired for install/uninstall
hooks, all Native AOT clean.

## Design decisions

- `VelopackUpdateService` in `Yaapic.Infrastructure/Updates` (or a small `Yaapic.Updates` project if the package must not
  reach Infrastructure; architecture tests decide). Source: `GithubSource("https://github.com/Yaapic/yaapic", null, prerelease)`
  where `prerelease` follows the channel.
- `VelopackApp.Build().Run()` is the first statement of both `Program.Main` methods (WinUI and Avalonia); it handles the
  installer hooks and exits early when Velopack asks.
- `IsSupported` is `UpdateManager.IsInstalled`.
- No reflection: if the package needs trimming roots, they go in the project with a one-line XML comment each.

## Out of scope

UI, packaging (R-08).

## Acceptance criteria

- [ ] AOT publish of both apps with the package: no new trim/AOT warnings (warnings are errors).
- [ ] Integration test against a local file source (a feed folder produced by `vpk pack` in the test fixture or a checked-in
      tiny fake feed): available, up to date, failed (unreachable) all map correctly.
- [ ] `NativeSmoke` still passes on the three OSes.

## Suggested commit message

`feat: add the Velopack update service`
