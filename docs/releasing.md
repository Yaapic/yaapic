# Releasing

Decisions: [ADR 0011](adr/0011-distribution-and-updates.md). The workflow is `.github/workflows/release.yml`.

## Cut a release

1. Make sure `main` is green and holds what you want to ship.
2. Tag it and push the tag: `vMAJOR.MINOR.PATCH` for the stable channel, `vMAJOR.MINOR.PATCH-preview.N` for the preview channel
   (any suffix makes a pre-release).

   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```

   Before tagging, rewrite `RELEASE_NOTES.md` at the repo root for this release: `vpk pack --releaseNotes` embeds it in the
   packages and installed apps show it in "Updates and about" when the update is offered.

3. The workflow runs on Windows, Linux and macOS, one after the other. Each one builds the native script engines, publishes the
   app with Native AOT (`-p:YaapicVersion=<tag without v>`), packs it with `vpk` and uploads the packages to a **draft** release
   named `Yaapic <version>`: Windows `Setup.exe` and portable zip, the macOS bundle package, the Linux AppImage, the full and delta
   packages, the `releases.<os>.json` feed files and `SHA256SUMS-<rid>.txt`.
4. Open the draft, check the assets (three `SHA256SUMS`, one `releases.*.json` per OS), write the notes, and publish it. Installed
   apps find it on their next check (once a day, or "Check for updates"). The `preview` channel sees pre-releases and stable
   releases, the `stable` channel only stable ones.

A bad release is withdrawn by deleting (or un-publishing) the GitHub release: apps only ever look at the published list.

## Dry run

Changing `release.yml` in a pull request, or running the workflow by hand, builds and packs everything as version
`0.0.1-dryrun.<run number>` and keeps the packages as a workflow artifact for three days. It never touches a release.

## Signing

| Platform | What is needed | Without it |
|---|---|---|
| Windows | Repository secrets `WINDOWS_CERT_BASE64` (the `.pfx`, base64) and `WINDOWS_CERT_PASSWORD`. | The installer is unsigned: SmartScreen shows "Windows protected your PC" until the file has reputation. |
| macOS | Not wired yet (needs an Apple Developer ID and notarization). | The bundle is unsigned: Gatekeeper asks the user to allow it in System Settings. |
| Linux | Nothing. | AppImage is not signed. |

Rotate the Windows certificate by replacing the two secrets; nothing else references them, and the workflow deletes the `.pfx` after
packing. Secrets are never printed (GitHub masks them) and never committed.

## Updating the pack tool

`VPK_VERSION` in the workflow and the `Velopack` package version in `Directory.Packages.props` move together. After bumping both,
run the workflow as a dry run, then publish a `-preview` tag and check an installed app updates to it.

## Checked by hand

- 2026-11-12: `vpk pack` of the WinUI Native AOT build produced `Setup.exe`, the portable zip and the full package; the app started from
  the packed (installed) layout, with the version in its title, and stayed responsive.
- Not checked until the first tag: the upload and `--merge` across the three jobs, delta packages from a previous release, a real
  update from one published version to the next, and packing on Linux and macOS.
