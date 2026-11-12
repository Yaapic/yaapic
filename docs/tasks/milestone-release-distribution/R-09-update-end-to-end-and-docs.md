# R-09: Update end-to-end tests and docs

Status: done
Platform: any
Depends on: R-06, R-07, R-08

## Goal

Close the distribution milestone: end-to-end coverage, user-facing docs, README install section.

## Design decisions

- End-to-end test through the shell ViewModel with the fake service: startup check, banner, download, restart-to-apply,
  channel switch, offline failure.
- README gets an "Install" section (Windows Setup, macOS, Linux AppImage, how updates work, how to disable them) and the
  contributor section links `docs/releasing.md`.
- `docs/dependencies.md` lists Velopack (version, licence, why).

## Out of scope

Store distribution.

## Acceptance criteria

- [x] Tests green locally on Windows (`UpdatesEndToEndTests`: start-up check, banner, download, restart, offline, channel switch and
      persistence); the three OSes are covered by CI.
- [x] Task index updated, milestone summary written with what was not checked by hand.

## Suggested commit message

`docs: document installation and updates, add end-to-end tests`
