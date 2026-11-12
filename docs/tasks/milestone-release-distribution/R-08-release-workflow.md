# R-08: Release workflow and packaging

Status: todo
Platform: any
Depends on: R-04, R-02

## Goal

Pushing a `v*` tag builds, packs and publishes installers and the update feed to a GitHub Release.

## Design decisions

- `.github/workflows/release.yml`, triggered by tags `v*.*.*`; matrix windows-latest (win-x64), macos-latest (osx-arm64),
  ubuntu-latest (linux-x64); same SDK pin and native build as `ci.yml`.
- Per OS: native build, AOT publish of the right app (WinUI on Windows, Avalonia elsewhere) with
  `-p:YaapicVersion=<tag without v>`, `vpk pack` (installed as a dotnet tool at a pinned version), `vpk upload github` with the
  channel from the tag suffix; release created as a draft, published by hand after a look.
- Signing steps run only when their secrets exist (`WINDOWS_CERT`, `APPLE_*`); otherwise they log "unsigned" and continue.
- The published artifacts include a SHA-256 list. `docs/releasing.md` explains how to cut a release, rotate secrets and
  what an unsigned build shows to the user.
- Pull requests that touch the workflow run it in dry mode (`workflow_dispatch` with `dry-run: true`, no upload).

## Out of scope

winget, Homebrew and Flathub manifests (follow-ups).

## Acceptance criteria

- [ ] A `v0.0.1-preview.1` tag produces a draft release with a Setup for each OS and a feed readable by R-04.
- [ ] A second tag `v0.0.1-preview.2` yields delta packages and an installed `.1` updates to `.2` (checked by hand on Windows,
      recorded in `docs/releasing.md`).
- [ ] No secret value appears in logs.

## Suggested commit message

`ci: add the tag-triggered release workflow`
