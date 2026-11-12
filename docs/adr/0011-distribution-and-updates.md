# ADR 0011: Distribution and automatic updates

Status: Accepted

See also: [ADR 0004](0004-http3-and-sdk.md). The cards R-02 to R-09 read this ADR first.

## Context

Yaapic is built per platform (WinUI 3 on Windows, Avalonia on macOS and Linux), always as Native AOT. Users need an
installer and updates that arrive without them downloading a new archive. There is no server of our own and no telemetry.

## Options considered

| Option | Verdict |
|---|---|
| Velopack | One `vpk` CLI for Windows, macOS and Linux, Setup.exe / macOS bundle / AppImage, delta packages, GitHub Releases as a feed. Chosen. |
| MSIX + App Installer | Windows only; macOS and Linux would still need another mechanism. Kept as the Windows fallback. |
| Plain archives, manual update | No update flow at all. Rejected. |

## Native AOT spike (2026-11-12)

Velopack 1.2.161 and `vpk` 1.2.161 on the .NET 11 SDK, win-x64. A console app with `PublishAot`, `IsAotCompatible` and
`TreatWarningsAsErrors` referencing `Velopack` (`VelopackApp.Build().Run()`, `UpdateManager`, `GithubSource`,
`SimpleFileSource`):

- AOT publish: no trim or AOT warning, native executable produced.
- `vpk pack` of 1.0.0 then 1.0.1 produced the Setup.exe, a portable zip, the full packages and a **delta** package.
- Run from the packed portable layout: `IsInstalled` is true, `CurrentVersion` is the packed one, a check against a local
  folder feed answers "up to date". Run un-installed, `IsInstalled` is false and a check throws `NotInstalledException`
  (so the adapter checks `IsInstalled` first, as R-03 plans with `IsSupported`).
- Not done: applying a real 1.0.0 to 1.0.1 update (R-08 does it with the release workflow), and macOS / Linux packing
  (built by the release workflow on its own runners only, like the native libraries in M3-15).

The fallback (MSIX on Windows) is therefore not needed now; R-04 and R-08 stand as written.

## Decision

- **Tool**: Velopack, packages pinned in `Directory.Packages.props` and `vpk` pinned as a dotnet tool version in the workflow.
- **Feed**: GitHub Releases of `Yaapic/yaapic`, created from a tag `vMAJOR.MINOR.PATCH[-suffix]`.
- **Channels**: `stable` (no suffix) and `preview` (any suffix, for example `-preview.2`). The channel is a user setting; the
  default is the channel of the running version. `preview` users also see stable releases newer than their version.
- **Version**: taken from the tag through the `YaapicVersion` MSBuild property; local builds are `0.0.0-dev` and
  are never offered an update (`IsSupported` is false when not installed).
- **Flow**: check at startup at most once per 24 hours and on demand; download in the background; apply on the next
  restart, never forced; a failed check is silent in the UI and logged in the console log.
- **Signing**: Authenticode (Windows) and notarization (macOS) run only when their secrets exist; unsigned builds are
  allowed and documented. No secret is committed or printed.
- **Privacy**: the only network call is the feed request to GitHub; no identifier or usage data is sent.
- **Releases are drafts**: the workflow uploads a draft, a maintainer publishes it after a look.

## Consequences

- Update packages are produced per platform, so a release needs all three runners to succeed.
- The Velopack startup hook must run before anything else in each `Main` (R-04).
- winget, Homebrew and Flathub manifests are follow-ups, not part of this milestone.
