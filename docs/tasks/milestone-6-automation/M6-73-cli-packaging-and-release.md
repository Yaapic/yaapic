# M6-73: CLI: CI smoke test, zip per OS and release upload

Status: todo
Platform: any
Depends on: M6-71

## Goal

Build `yaapic-cli` with Native AOT on the three OSes in CI, and ship it as one zip per OS on the GitHub Release (ADR 0022), next to the
Velopack packages of the app.

## Design decisions

- Read first: ADR 0022 ("Distribution"), ADR 0011, `.github/workflows/ci.yml` (matrix, "Publish with Native AOT", "Smoke test the published
  binary"), `.github/workflows/release.yml` (version step, natives, "Checksums", "Upload to the release", dry-run artifact),
  `docs/releasing.md`, R-08.
- `ci.yml`, after the app publish, a step "Publish and smoke test yaapic-cli (Native AOT)" (pwsh):
  `dotnet publish src/Yaapic.Cli -c Release -r ${{ matrix.rid }} -p:PublishAot=true -o cli`; then, on the published exe (`chmod +x` off
  Windows): `--version` exits 0 and prints `yaapic-cli `; `--help` exits 0; `run <a directory that does not exist> --folder X` exits 3;
  `run` with no folder exits 2; the QuickJS-NG and Lua libraries are in `cli/` (fail the step with the missing name otherwise).
- `release.yml`, after "Pack (Linux and macOS)" and before "Checksums":
  - "Publish yaapic-cli": same publish command with `-p:YaapicVersion=${{ steps.version.outputs.version }} -o cli`; delete `*.pdb` and
    `*.dbg` from `cli/`; copy `LICENSE` into `cli/` (the native `THIRD-PARTY-NOTICES.md` already comes with `Yaapic.Scripting`).
  - "Zip yaapic-cli": `cli-out/yaapic-cli-<version>-<rid>.zip` with the files at the root of the zip. Windows: `Compress-Archive cli/*`.
    Linux and macOS: `cd cli && zip -r ../cli-out/<name>.zip .` (keeps the executable bit; `Compress-Archive` would drop it).
  - "Checksums": also hash `cli-out/*.zip` into the same `SHA256SUMS-<rid>.txt`.
  - After "Upload to the release": "Upload yaapic-cli" (`if: env.IS_RELEASE == 'true'`):
    `gh release upload ${{ github.ref_name }} cli-out/yaapic-cli-<version>-<rid>.zip --clobber`.
  - The dry-run artifact also keeps `cli-out/` (add the path to `upload-artifact`).
- No Velopack for the CLI, no signing (the app's Windows certificate step is not reused; say so in the docs card). On macOS an unsigned
  binary downloaded by a browser is quarantined: the docs (M6-74) give `xattr -d com.apple.quarantine yaapic-cli`.
- `docs/releasing.md`: in "Cut a release" step 3, add the `yaapic-cli-<version>-<rid>.zip` asset per OS; in step 4, the check "one
  `yaapic-cli-*.zip` per OS"; in "Checked by hand", a line saying the CLI zips were checked only in a dry run until the first real tag.

## Out of scope

winget, Homebrew or apt packages, signing and notarization, a Docker image.

## Acceptance criteria

- [ ] The CI workflow passes on the pull request with the new step on the three OSes (or, if CI cannot run from this session, the step was
  run locally on the host OS with the same commands; write which in the Notes).
- [ ] A dry run of `release.yml` (it runs on a pull request that changes it) produces a `dry-run-<rid>` artifact with the zip on each OS;
  the Linux zip, unzipped with `unzip`, keeps `yaapic-cli` executable. If the dry run cannot be triggered from this session, check the
  zip commands locally and say so in the Notes.
- [ ] `docs/releasing.md` updated as above.

## Suggested commit message

`ci: build yaapic-cli and attach a zip per OS to releases`
