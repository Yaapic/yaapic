# R-01: ADR 0011, installer and automatic updates

Status: todo
Platform: any
Depends on: none

## Goal

Record the distribution decisions before any code: tool, channels, versioning, signing, update flow.

## Design decisions

- Velopack packs and updates all three platforms (Windows Setup.exe, macOS bundle, Linux AppImage) from one `vpk` CLI, with
  delta packages and silent updates. The ADR first confirms by a short spike (a hello-world AOT binary packed and updated
  locally) that it works with Native AOT; if not, it records the fallback (MSIX + App Installer on Windows, plain archives
  elsewhere) and the cards R-04 and R-08 are rewritten.
- Releases are GitHub Releases of `Yaapic/yaapic`, created from a `vMAJOR.MINOR.PATCH[-pre]` tag. The update feed is that
  release list; no server of our own.
- Channels: `stable` (tags without suffix) and `preview` (tags with a suffix). The user picks the channel in settings.
- The app version comes from the tag (`Version` MSBuild property), is shown in the UI and sent nowhere.
- Update checks: at startup (at most once a day) and on demand; downloads in the background; the update is applied on the
  next restart, never forced. A failing check is silent in the UI, logged in the console log only.
- Signing: Authenticode on Windows and notarization on macOS when the secrets exist; unsigned builds are allowed and
  documented (SmartScreen / Gatekeeper warnings). No secret is ever committed.
- No telemetry.

## Out of scope

Any code, the workflow, store distribution (winget, Homebrew, Flathub: listed as follow-ups).

## Acceptance criteria

- [ ] `docs/adr/0011-distribution-and-updates.md` written and linked from `docs/adr/README.md`.
- [ ] The Native AOT spike result is recorded (what was tried, version of Velopack, outcome).

## Suggested commit message

`docs: add ADR 0011 for installer and automatic updates`
